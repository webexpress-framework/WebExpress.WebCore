using WebExpress.WebCore.Test.Fixture;
using WebExpress.WebCore.WebApplication;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebCore.WebHealt;

namespace WebExpress.WebCore.Test.Data
{
    /// <summary>
    /// Exercises public sealed component discovery, dependency injection, and optional disposal.
    /// </summary>
    public sealed class TestHealth : IHealth, IDisposable
    {
        private readonly HealthTestPluginContext _plugin;

        /// <summary>
        /// Verifies that component activation supplies the binding and framework dependencies.
        /// </summary>
        /// <param name="healthContext">The plugin and application binding under test.</param>
        /// <param name="applicationContext">The application injected independently of the health context.</param>
        /// <param name="componentHub">The hub supplying framework managers.</param>
        /// <param name="httpServerContext">The host configuration and diagnostic context.</param>
        /// <param name="healthManager">The shared manager injected through the component registry.</param>
        /// <param name="componentId">The discovered component identifier.</param>
        private TestHealth(IHealthContext healthContext, IApplicationContext applicationContext,
            IComponentHub componentHub, IHttpServerContext httpServerContext, IHealthManager healthManager,
            IComponentId componentId)
        {
            Assert.Same(healthContext.ApplicationContext, applicationContext);
            Assert.Same(componentHub.HealthManager, healthManager);
            Assert.Same(healthContext.HealthId, componentId);
            Assert.NotNull(httpServerContext);
            _plugin = healthContext.PluginContext as HealthTestPluginContext;
            if (_plugin is not null)
            {
                lock (_plugin)
                {
                    _plugin.Created++;
                    _plugin.ActivatedContexts.Add(healthContext);
                }

                if (_plugin.FailConstruction)
                {
                    throw new InvalidOperationException("private-constructor-diagnostic");
                }
            }
        }

        /// <summary>
        /// Executes configurable fixture behavior while default plugin discovery remains healthy.
        /// </summary>
        /// <param name="cancellationToken">The budget token supplied by the health manager.</param>
        /// <returns>The configured dependency result or a successful default.</returns>
        public Task<HealthCheckResult> CheckAsync(CancellationToken cancellationToken)
        {
            return _plugin is null ? Task.FromResult(HealthCheckResult.Healthy()) : _plugin.Check(cancellationToken);
        }

        /// <summary>
        /// Records cleanup so tests can detect premature or duplicate disposal.
        /// </summary>
        public void Dispose()
        {
            if (_plugin is not null)
            {
                lock (_plugin)
                {
                    _plugin.Disposed++;
                    _plugin.DisposedSignal.TrySetResult();
                }
            }
        }
    }

    /// <summary>
    /// Supplies closed generic fixture types for discovery filtering and invalid timeout tests.
    /// </summary>
    /// <typeparam name="T">The fixture type argument used to close the otherwise undiscoverable type.</typeparam>
    [HealthTimeout(0)]
    public sealed class InvalidTimeoutHealth<T> : IHealth
    {
        /// <summary>
        /// Fails the test if invalid component metadata is silently ignored.
        /// </summary>
        /// <param name="cancellationToken">The unused dependency cancellation token.</param>
        /// <returns>No result because invalid metadata must prevent invocation.</returns>
        public Task<HealthCheckResult> CheckAsync(CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("Invalid metadata must prevent execution.");
        }
    }

    /// <summary>
    /// Supplies an explicitly bounded component without polluting ordinary assembly discovery.
    /// </summary>
    /// <typeparam name="T">The fixture type argument used to close the component.</typeparam>
    [HealthTimeout(25)]
    public sealed class TimedHealth<T> : IHealth
    {
        /// <summary>
        /// Waits for the declared budget to demonstrate attribute-driven cancellation.
        /// </summary>
        /// <param name="cancellationToken">The token governed by the timeout attribute.</param>
        /// <returns>A task cancelled by the component's declared budget.</returns>
        public async Task<HealthCheckResult> CheckAsync(CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return HealthCheckResult.Healthy();
        }
    }

    /// <summary>
    /// Ensures the public sealed discovery convention rejects extensible helper classes.
    /// </summary>
    public class UnsealedHealth : IHealth
    {
        /// <summary>
        /// Makes accidental discovery visible as a failed probe.
        /// </summary>
        /// <param name="cancellationToken">The unused check cancellation token.</param>
        /// <returns>An unhealthy result that must never participate in normal discovery.</returns>
        public Task<HealthCheckResult> CheckAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy());
        }
    }
}
