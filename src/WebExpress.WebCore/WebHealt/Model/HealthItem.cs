using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebComponent;

namespace WebExpress.WebCore.WebHealt.Model
{
    /// <summary>
    /// Owns one application binding and shares unfinished checks to bound work when cancellation is ignored.
    /// </summary>
    internal sealed class HealthItem : IDisposable
    {
        private readonly IComponentHub _componentHub;
        private readonly IHttpServerContext _httpServerContext;
        private readonly Exception _activationError;
        private readonly Lock _sync = new();
        private IHealth _instance;
        private Task<bool> _operation;
        private bool _disposed;

        /// <summary>
        /// Gets the metadata retained even when component activation fails.
        /// </summary>
        public IHealthContext HealthContext { get; }

        /// <summary>
        /// Gets the discovered type used to distinguish bindings during lifecycle events.
        /// </summary>
        public Type HealthClass { get; }

        /// <summary>
        /// Keeps discovery separate from activation so failing constructors cannot disappear from aggregate health.
        /// </summary>
        /// <param name="healthClass">The discovered public sealed health component.</param>
        /// <param name="healthContext">The plugin and application ownership of this binding.</param>
        /// <param name="componentHub">The component hub used for constructor injection.</param>
        /// <param name="httpServerContext">The host supplying private diagnostics and configuration.</param>
        public HealthItem(Type healthClass, HealthContext healthContext, IComponentHub componentHub,
            IHttpServerContext httpServerContext)
        {
            HealthClass = healthClass;
            HealthContext = healthContext;
            _componentHub = componentHub;
            _httpServerContext = httpServerContext;

            try
            {
                var timeout = healthClass.GetCustomAttribute<HealthTimeoutAttribute>();
                if (timeout is not null)
                {
                    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(timeout.Milliseconds);
                    healthContext.Timeout = TimeSpan.FromMilliseconds(timeout.Milliseconds);
                }
            }
            catch (Exception ex)
            {
                _activationError = ex;
            }
        }

        /// <summary>
        /// Limits probe latency even when component construction or synchronous application code blocks.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token for this caller, independent of other probes.</param>
        /// <returns>True only when the dependency succeeds within its budget.</returns>
        public async Task<bool> CheckAsync(CancellationToken cancellationToken)
        {
            Task<bool> operation;
            lock (_sync)
            {
                if (_disposed)
                {
                    return false;
                }

                if (_operation is null || _operation.IsCompleted)
                {
                    _operation = Task.Run(ExecuteAsync);
                }

                operation = _operation;
            }

            try
            {
                return await operation.WaitAsync(HealthContext.Timeout, cancellationToken);
            }
            catch (TimeoutException)
            {
                _httpServerContext.Log?.Error(I18N.Translate("webexpress.webcore:health.timeout",
                    HealthContext.ApplicationContext.ApplicationId, HealthContext.HealthId, HealthContext.Timeout));
                return false;
            }
        }

        /// <summary>
        /// Reuses one injected component per binding and observes exceptions after a probe has timed out.
        /// </summary>
        /// <returns>True only for an explicit successful result before cancellation.</returns>
        private async Task<bool> ExecuteAsync()
        {
            using var cancellation = new CancellationTokenSource(HealthContext.Timeout);
            try
            {
                if (_activationError is not null)
                {
                    throw new InvalidOperationException("The health component has invalid configuration.", _activationError);
                }

                _instance ??= ComponentActivator.CreateInstance<IHealth, IHealthContext>
                (
                    HealthClass, HealthContext, _httpServerContext, _componentHub, HealthContext.ApplicationContext
                ) ?? throw new InvalidOperationException("The health component could not be created.");

                var result = await _instance.CheckAsync(cancellation.Token);
                cancellation.Token.ThrowIfCancellationRequested();
                if (result?.IsHealthy == true)
                {
                    return true;
                }

                _httpServerContext.Log?.Error(I18N.Translate("webexpress.webcore:health.failed",
                    HealthContext.ApplicationContext.ApplicationId, HealthContext.HealthId,
                    result?.Description ?? "The check did not confirm availability."));
            }
            catch (Exception ex)
            {
                _httpServerContext.Log?.Error(I18N.Translate("webexpress.webcore:health.failed",
                    HealthContext.ApplicationContext.ApplicationId, HealthContext.HealthId, ex.ToString()));
                _httpServerContext.Log?.Exception(ex);
            }

            return false;
        }

        /// <summary>
        /// Prevents new invocations and defers resource disposal until an active invocation finishes.
        /// </summary>
        public void Dispose()
        {
            Task<bool> operation;
            lock (_sync)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                operation = _operation;
            }

            _ = DisposeAfterExecutionAsync(operation);
        }

        /// <summary>
        /// Releases component resources without racing a check that is still using them.
        /// </summary>
        /// <param name="operation">The last invocation, or null when the component was never activated.</param>
        /// <returns>A task that completes after the component's resources have been released.</returns>
        private async Task DisposeAfterExecutionAsync(Task<bool> operation)
        {
            try
            {
                if (operation is not null)
                {
                    await operation;
                }
            }
            finally
            {
                try
                {
                    (_instance as IDisposable)?.Dispose();
                }
                catch (Exception ex)
                {
                    _httpServerContext.Log?.Exception(ex);
                }
            }
        }
    }
}
