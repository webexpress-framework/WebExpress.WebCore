using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WebExpress.WebCore.WebLog;

namespace WebExpress.WebCore
{
    /// <summary>
    /// Keeps background work alive until the host has drained it before releasing shared resources.
    /// </summary>
    public sealed class ServerLifetime
    {
        private readonly Lock _sync = new();
        private readonly HashSet<Task> _work = [];
        private readonly CancellationTokenSource _stopping = new();
        private readonly CancellationToken _stoppingToken;
        private readonly ILog _log;
        private Task _drainTask;

        /// <summary>
        /// Gets the signal for stopping producers and long-running loops. An operation already
        /// committing data may finish without observing this token.
        /// </summary>
        public CancellationToken Stopping => _stoppingToken;

        /// <summary>
        /// Initializes a lifetime associated with the host's diagnostic log.
        /// </summary>
        /// <param name="log">The log that receives failures from background work.</param>
        public ServerLifetime(ILog log)
        {
            _log = log;
            _stoppingToken = _stopping.Token;
        }

        /// <summary>
        /// Admits synchronous background work only while the host accepts new work.
        /// </summary>
        /// <param name="work">The operation whose resources must remain available until it returns.</param>
        /// <returns>True when admitted, or false when shutdown has already started.</returns>
        public bool TryRun(Action work)
        {
            ArgumentNullException.ThrowIfNull(work);
            return TryRun(_ =>
            {
                work();
                return Task.CompletedTask;
            });
        }

        /// <summary>
        /// Admits asynchronous background work and observes its completion, including failures.
        /// </summary>
        /// <param name="work">The operation, with a token for stopping producers during shutdown.</param>
        /// <returns>True when admitted, or false when shutdown has already started.</returns>
        public bool TryRun(Func<CancellationToken, Task> work)
        {
            ArgumentNullException.ThrowIfNull(work);
            lock (_sync)
            {
                if (_drainTask is not null)
                {
                    return false;
                }

                var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                _work.Add(completion.Task);
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await work(Stopping).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (Stopping.IsCancellationRequested)
                    {
                        // cooperative loop termination is an expected shutdown outcome
                    }
                    catch (Exception ex)
                    {
                        _log?.Exception(ex);
                    }
                    finally
                    {
                        lock (_sync)
                        {
                            _work.Remove(completion.Task);
                            completion.SetResult();
                        }
                    }
                });
                return true;
            }
        }

        /// <summary>
        /// Closes admission, signals producers, and waits for every operation admitted beforehand.
        /// Repeated callers observe the same drain without signaling callbacks again.
        /// </summary>
        /// <param name="cancellationToken">The host's remaining shutdown budget.</param>
        /// <returns>A task that completes when work drains, or is canceled when the budget expires.</returns>
        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            lock (_sync)
            {
                if (_drainTask is null)
                {
                    // publish the closed gate before cancellation callbacks can submit more work
                    var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                    _drainTask = completion.Task;
                    _ = DrainAsync(_work.ToArray(), completion);
                }

                return _drainTask.WaitAsync(cancellationToken);
            }
        }

        /// <summary>
        /// Observes callback failures without letting one producer skip the remaining operations.
        /// </summary>
        /// <param name="work">The operations admitted before closing the gate.</param>
        /// <param name="completion">The shared completion observed by shutdown callers.</param>
        /// <returns>A task that completes after producers and admitted work finish.</returns>
        private async Task DrainAsync(Task[] work, TaskCompletionSource completion)
        {
            try
            {
                await _stopping.CancelAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _log?.Exception(ex);
            }

            await Task.WhenAll(work).ConfigureAwait(false);
            _stopping.Dispose();
            completion.SetResult();
        }
    }
}
