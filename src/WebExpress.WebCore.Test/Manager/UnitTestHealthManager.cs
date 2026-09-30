using System.Reflection;
using WebExpress.WebCore.Test.Data;
using static WebExpress.WebCore.Test.Fixture.HealthTestFixture;
using WebExpress.WebCore.Test.Fixture;
using WebExpress.WebCore.WebApplication;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebCore.WebHealt;
using WebExpress.WebCore.WebLog;

namespace WebExpress.WebCore.Test.Manager
{
    /// <summary>
    /// Verifies that framework state and every critical dependency participate in aggregate health.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestHealthManager
    {
        /// <summary>
        /// Discovers components through real plugin events and rebinds applications added after their plugin.
        /// </summary>
        /// <returns>A task that completes after both lifecycle orders have been validated.</returns>
        [Fact]
        public async Task PluginAndApplicationEvents_DiscoverChecksAutomatically()
        {
            // arrange
            var (hub, _) = CreateHost();
            using var manager = hub.HealthManager;
            var additions = new List<IHealthContext>();
            var removals = new List<IHealthContext>();
            manager.AddHealth += (_, context) => additions.Add(context);
            manager.RemoveHealth += (_, context) => removals.Add(context);

            // act
            ((WebPlugin.PluginManager)hub.PluginManager).Register();
            var plugin = hub.PluginManager.GetPlugin(typeof(TestPlugin));
            var first = manager.HealthChecks.ToArray();
            var initialHealth = await manager.CheckAsync(TestContext.Current.CancellationToken);
            ((ApplicationManager)hub.ApplicationManager).Remove(plugin);
            var empty = manager.HealthChecks.ToArray();
            typeof(ApplicationManager).GetMethod("Register", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(hub.ApplicationManager, [plugin]);
            var rebound = manager.HealthChecks.ToArray();
            var reboundHealth = await manager.CheckAsync(TestContext.Current.CancellationToken);

            // validation
            Assert.Equal(3, first.Length);
            Assert.Equal(3, rebound.Length);
            Assert.Equal(6, additions.Count);
            Assert.Equal(3, removals.Count);
            Assert.Empty(empty);
            Assert.True(initialHealth);
            Assert.True(reboundHealth);
            Assert.All(first, x => Assert.Equal(typeof(TestHealth).FullName.ToLowerInvariant(), x.HealthId.ToString()));
            Assert.All(rebound, x => Assert.Contains(x.ApplicationContext, hub.ApplicationManager.Applications));
            Assert.All(first, x => Assert.DoesNotContain(x.ApplicationContext, rebound.Select(y => y.ApplicationContext)));
        }

        /// <summary>
        /// Avoids duplicate bindings and reuses a separate injected instance for each application.
        /// </summary>
        /// <returns>A task that completes after discovery, activation, and disposal are validated.</returns>
        [Fact]
        public async Task Discovery_IsIdempotentAndInstancesBelongToApplications()
        {
            // arrange
            var (hub, _) = CreateHost();
            using var manager = hub.HealthManager;
            var plugin = new HealthTestPluginContext("plugin", typeof(TestHealth));
            var first = new ApplicationContext { ApplicationId = "first" };
            var second = new ApplicationContext { ApplicationId = "second" };
            var added = 0;
            var removed = 0;
            manager.AddHealth += (_, _) => added++;
            manager.RemoveHealth += (_, _) => removed++;

            // act
            ((HealthManager)manager).Register(plugin, [first, second, first]);
            ((HealthManager)manager).Register(plugin, [first, second]);
            var firstContexts = manager.GetHealthChecks(first).ToArray();
            var secondContexts = manager.GetHealthChecks(second).ToArray();
            var healthy = await manager.CheckAsync(TestContext.Current.CancellationToken);
            var repeated = await manager.CheckAsync(TestContext.Current.CancellationToken);
            ((HealthManager)manager).Remove(plugin);
            ((HealthManager)manager).Remove(plugin);

            // validation
            Assert.True(healthy);
            Assert.True(repeated);
            Assert.Single(firstContexts);
            Assert.Single(secondContexts);
            Assert.Equal(TimeSpan.FromSeconds(5), firstContexts[0].Timeout);
            Assert.Equal(2, added);
            Assert.Equal(2, removed);
            Assert.Equal(2, plugin.Created);
            Assert.Equal(2, plugin.Disposed);
            Assert.Equal(2, plugin.ActivatedContexts.Count);
            Assert.Empty(manager.HealthChecks);
        }

        /// <summary>
        /// Limits discovery to public sealed concrete components with closed type parameters.
        /// </summary>
        [Fact]
        public void Discovery_IgnoresUnsupportedTypes()
        {
            // arrange
            var (hub, _) = CreateHost();
            using var manager = hub.HealthManager;
            var plugin = new HealthTestPluginContext("plugin", typeof(TestHealth), typeof(UnsealedHealth),
                typeof(InvalidTimeoutHealth<>), typeof(IHealth), typeof(HealthTestRegistration));

            // act
            ((HealthManager)manager).Register(plugin, [new ApplicationContext()]);

            // validation
            Assert.Equal(typeof(TestHealth).FullName.ToLowerInvariant(), Assert.Single(manager.HealthChecks).HealthId.ToString());
            Assert.Equal(0, plugin.Created);
        }

        /// <summary>
        /// Applies declarative budgets while retaining invalid declarations as unsuccessful checks.
        /// </summary>
        /// <param name="invalid">Whether the component declares a nonpositive timeout.</param>
        /// <returns>A task that completes after the component budget is validated.</returns>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Discovery_TimeoutAttribute_GovernsProbeResult(bool invalid)
        {
            // arrange
            var (hub, server) = CreateHost();
            using var manager = hub.HealthManager;
            var plugin = new HealthTestPluginContext("plugin", invalid
                ? typeof(InvalidTimeoutHealth<int>) : typeof(TimedHealth<int>));

            // act
            ((HealthManager)manager).Register(plugin, [new ApplicationContext()]);
            var healthy = await manager.CheckAsync(TestContext.Current.CancellationToken);

            // validation
            Assert.False(healthy);
            Assert.Equal(invalid ? TimeSpan.FromSeconds(5) : TimeSpan.FromMilliseconds(25), Assert.Single(manager.HealthChecks).Timeout);
            if (invalid)
            {
                Assert.Contains(server.HttpServerContext.Log.GetRecentEntries(), x => x.Message.Contains("invalid configuration"));
            }
        }

        /// <summary>
        /// Keeps activation failures visible to probes instead of silently losing a critical component.
        /// </summary>
        /// <returns>A task that completes after failure and later activation recovery are validated.</returns>
        [Fact]
        public async Task Discovery_ConstructorFails_RemainsUnhealthyAndCanRecover()
        {
            // arrange
            var (hub, server) = CreateHost();
            using var manager = hub.HealthManager;
            var plugin = new HealthTestPluginContext("plugin", typeof(TestHealth)) { FailConstruction = true };
            ((HealthManager)manager).Register(plugin, [new ApplicationContext()]);

            // act
            var failed = await manager.CheckAsync(TestContext.Current.CancellationToken);
            plugin.FailConstruction = false;
            var recovered = await manager.CheckAsync(TestContext.Current.CancellationToken);

            // validation
            Assert.False(failed);
            Assert.True(recovered);
            Assert.Single(manager.HealthChecks);
            Assert.Equal(2, plugin.Created);
            Assert.Contains(server.HttpServerContext.Log.GetRecentEntries(), x => x.Message.Contains("private-constructor-diagnostic"));
        }

        /// <summary>
        /// Keeps a declared application whose constructor threw visible to probes, although it left no
        /// health bindings behind, until the plugin that declares it is removed.
        /// </summary>
        /// <returns>A task that completes after failure, diagnostics, and recovery are validated.</returns>
        [Fact]
        public async Task ApplicationConstructorFails_IsUnhealthyUntilPluginRemoved()
        {
            // arrange
            var (hub, server) = CreateHost();
            using var manager = hub.HealthManager;
            var applications = (ApplicationManager)hub.ApplicationManager;
            var type = CreateThrowingApplication("private-application-diagnostic");
            var plugin = new HealthTestPluginContext("failing-plugin", type);

            // act
            typeof(ApplicationManager).GetMethod("Register", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(applications, [plugin]);
            var failed = await manager.CheckAsync(TestContext.Current.CancellationToken);
            var live = manager.CheckLiveness();
            var failure = Assert.Single(applications.FailedApplications);
            applications.Remove(plugin);
            var recovered = await manager.CheckAsync(TestContext.Current.CancellationToken);

            // validation
            Assert.False(failed);
            Assert.False(live);
            Assert.True(recovered);
            Assert.Equal(type.FullName.ToLower(), failure.ApplicationId);
            Assert.Same(plugin, failure.PluginContext);
            Assert.Equal("private-application-diagnostic", Assert.IsType<InvalidOperationException>(failure.Exception).Message);
            Assert.Empty(applications.GetApplications(plugin));
            Assert.Empty(applications.FailedApplications);
            Assert.Contains(server.HttpServerContext.Log.GetRecentEntries(), x =>
                x.Message.Contains(failure.ApplicationId) && x.Message.Contains("private-application-diagnostic"));
        }

        /// <summary>
        /// Keeps the liveness probe independent of application dependencies while still judging the framework.
        /// </summary>
        /// <returns>A task that completes after readiness and liveness are compared.</returns>
        [Fact]
        public async Task CheckLiveness_SkipsApplicationChecks()
        {
            // arrange
            var (hub, server) = CreateHost();
            using var manager = hub.HealthManager;
            var calls = 0;
            Register(manager, new ApplicationContext(), "database", _ =>
            {
                Interlocked.Increment(ref calls);
                return Task.FromResult(HealthCheckResult.Unhealthy());
            });

            // act
            var ready = await manager.CheckAsync(TestContext.Current.CancellationToken);
            var callsAfterReadiness = calls;
            var live = manager.CheckLiveness();
            SetRunning(server, false);
            var stopping = manager.CheckLiveness();

            // validation
            Assert.False(ready);
            Assert.True(live);
            Assert.False(stopping);
            Assert.Equal(callsAfterReadiness, calls);
        }

        /// <summary>
        /// Removes checks contributed by another plugin without removing the application or unrelated checks.
        /// </summary>
        /// <returns>A task that completes after the actual plugin removal event has been processed.</returns>
        [Fact]
        public async Task RemovePlugin_DetachesOnlyItsContribution()
        {
            // arrange
            var (hub, _) = CreateHost();
            using var manager = hub.HealthManager;
            ((WebPlugin.PluginManager)hub.PluginManager).Register();
            var application = hub.ApplicationManager.Applications.First();
            using var extension = Register(manager, application, "extension", _ => Task.FromResult(HealthCheckResult.Unhealthy()));

            // act
            var failed = await manager.CheckAsync(TestContext.Current.CancellationToken);
            ((WebPlugin.PluginManager)hub.PluginManager).Remove(extension.Plugin);
            var healthy = await manager.CheckAsync(TestContext.Current.CancellationToken);

            // validation
            Assert.False(failed);
            Assert.True(healthy);
            Assert.Contains(application, hub.ApplicationManager.Applications);
            Assert.DoesNotContain(manager.HealthChecks, x => x.PluginContext == extension.Plugin);
            Assert.Equal(1, extension.Plugin.Disposed);
        }

        /// <summary>
        /// Delays resource disposal until a detached component's active invocation finishes.
        /// </summary>
        /// <returns>A task that completes after deferred cleanup is validated.</returns>
        [Fact]
        public async Task RemovePlugin_InFlightCheck_DefersInstanceDisposal()
        {
            // arrange
            var (hub, _) = CreateHost();
            using var manager = hub.HealthManager;
            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var completion = new TaskCompletionSource<HealthCheckResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var registration = Register(manager, new ApplicationContext(), "plugin", _ =>
            {
                entered.TrySetResult();
                return completion.Task;
            });

            // act
            var probe = manager.CheckAsync(TestContext.Current.CancellationToken);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            registration.Dispose();
            var disposedWhileRunning = registration.Plugin.Disposed;
            completion.SetResult(HealthCheckResult.Healthy());
            var result = await probe;
            await registration.Plugin.DisposedSignal.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

            // validation
            Assert.Equal(0, disposedWhileRunning);
            Assert.Equal(1, registration.Plugin.Disposed);
            Assert.False(result);
            Assert.Empty(manager.HealthChecks);
        }

        /// <summary>
        /// Ensures a running framework is sufficient when an application has no external dependencies.
        /// </summary>
        /// <returns>A task that completes after aggregate health is validated.</returns>
        [Fact]
        public async Task CheckAsync_RunningFrameworkWithoutDependencies_IsHealthy()
        {
            // arrange
            var (hub, _) = CreateHost();
            using var manager = hub.HealthManager;

            // act
            var healthy = await manager.CheckAsync(TestContext.Current.CancellationToken);

            // validation
            Assert.True(healthy);
            Assert.Contains(manager, hub.Managers);
        }

        /// <summary>
        /// Keeps startup and shutdown out of the healthy state even when dependencies succeed.
        /// </summary>
        /// <returns>A task that completes after lifecycle health is validated.</returns>
        [Fact]
        public async Task CheckAsync_HostNotRunning_IsUnhealthy()
        {
            // arrange
            var (hub, server) = CreateHost();
            using var manager = hub.HealthManager;
            SetRunning(server, false);

            // act
            var healthy = await manager.CheckAsync(TestContext.Current.CancellationToken);

            // validation
            Assert.False(healthy);
            Assert.Contains(server.HttpServerContext.Log.GetRecentEntries(), x => x.Message.Contains("starting or stopping"));
        }

        /// <summary>
        /// Detects component initialization failures independently of listener reachability.
        /// </summary>
        /// <returns>A task that completes after the failed component is detected.</returns>
        [Fact]
        public async Task CheckAsync_MissingComponent_IsUnhealthy()
        {
            // arrange
            var (hub, _) = CreateHost();
            using var manager = hub.HealthManager;
            typeof(ComponentHub).GetField("_jobManager", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(hub, null);

            // act
            var healthy = await manager.CheckAsync(TestContext.Current.CancellationToken);

            // validation
            Assert.False(healthy);
        }

        /// <summary>
        /// Requires explicit success from every application while preserving diagnostics in the log.
        /// </summary>
        /// <param name="failure">The dependency failure mode exercised by the probe.</param>
        /// <returns>A task that completes after aggregation and logging are validated.</returns>
        [Theory]
        [InlineData("result")]
        [InlineData("throw")]
        [InlineData("faulted")]
        [InlineData("null")]
        [InlineData("null-task")]
        [InlineData("cancelled")]
        public async Task CheckAsync_AnyFailure_ExecutesAllChecksAndLogsDetails(string failure)
        {
            // arrange
            var (hub, server) = CreateHost();
            using var manager = hub.HealthManager;
            var application = new ApplicationContext { ApplicationId = "private-application" };
            var successfulCalls = 0;
            Register(manager, application, "healthy", _ =>
            {
                Interlocked.Increment(ref successfulCalls);
                return Task.FromResult(HealthCheckResult.Healthy());
            });
            Register(manager, application, "private-database", _ => failure switch
            {
                "result" => Task.FromResult(HealthCheckResult.Unhealthy("secret-diagnostic")),
                "throw" => throw new InvalidOperationException("secret-diagnostic"),
                "faulted" => Task.FromException<HealthCheckResult>(new InvalidOperationException("secret-diagnostic")),
                "null" => Task.FromResult<HealthCheckResult>(null),
                "null-task" => null,
                _ => Task.FromCanceled<HealthCheckResult>(new CancellationToken(true))
            });

            // act
            var healthy = await manager.CheckAsync(TestContext.Current.CancellationToken);

            // validation
            Assert.False(healthy);
            Assert.Equal(1, successfulCalls);
            Assert.Contains(server.HttpServerContext.Log.GetRecentEntries(), x => x.Message.Contains("private-application/" + typeof(TestHealth).FullName.ToLowerInvariant()));
            if (failure is "result" or "throw" or "faulted")
            {
                Assert.Contains(server.HttpServerContext.Log.GetRecentEntries(), x => x.Message.Contains("secret-diagnostic"));
            }
            if (failure is "throw" or "faulted")
            {
                Assert.Contains(server.HttpServerContext.Log.GetRecentEntries(), x =>
                    x.Level == LogLevel.Exception && x.Message.Contains("HealthItem.ExecuteAsync"));
            }
        }

        /// <summary>
        /// Reevaluates recovered dependencies instead of caching an unhealthy decision indefinitely.
        /// </summary>
        /// <returns>A task that completes after both probe decisions are validated.</returns>
        [Fact]
        public async Task CheckAsync_DependencyRecovers_UsesFreshResult()
        {
            // arrange
            var (hub, _) = CreateHost();
            using var manager = hub.HealthManager;
            var available = false;
            Register(manager, new ApplicationContext(), "database", _ => Task.FromResult(available
                ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy()));

            // act
            var failed = await manager.CheckAsync(TestContext.Current.CancellationToken);
            available = true;
            var recovered = await manager.CheckAsync(TestContext.Current.CancellationToken);

            // validation
            Assert.False(failed);
            Assert.True(recovered);
        }

        /// <summary>
        /// Bounds latency and repeated work even when a dependency ignores cancellation.
        /// </summary>
        /// <param name="synchronous">Whether the callback blocks before returning its task.</param>
        /// <returns>A task that completes after probe timeouts and invocation counts are validated.</returns>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task CheckAsync_UnresponsiveDependency_TimesOutWithoutOverlappingWork(bool synchronous)
        {
            // arrange
            var (hub, server) = CreateHost();
            using var manager = hub.HealthManager;
            var completion = new TaskCompletionSource<HealthCheckResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var calls = 0;
            Register(manager, new ApplicationContext { ApplicationId = "application" }, "blocked", _ =>
            {
                Interlocked.Increment(ref calls);
                entered.TrySetResult();
                return synchronous ? Task.FromResult(completion.Task.GetAwaiter().GetResult()) : completion.Task;
            }, TimeSpan.FromMilliseconds(100));

            try
            {
                // act
                var first = manager.CheckAsync(TestContext.Current.CancellationToken);
                await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
                var second = manager.CheckAsync(TestContext.Current.CancellationToken);
                var results = await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
                var repeated = await manager.CheckAsync(TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

                // validation
                Assert.All(results, Assert.False);
                Assert.False(repeated);
                Assert.Equal(1, calls);
                Assert.Contains(server.HttpServerContext.Log.GetRecentEntries(), x => x.Message.Contains("time budget"));
            }
            finally
            {
                completion.TrySetResult(HealthCheckResult.Healthy());
            }
        }

        /// <summary>
        /// Lets dependencies release resources when their check budget expires.
        /// </summary>
        /// <returns>A task that completes after the dependency observes cancellation.</returns>
        [Fact]
        public async Task CheckAsync_Timeout_CancelsDependencyToken()
        {
            // arrange
            var (hub, _) = CreateHost();
            using var manager = hub.HealthManager;
            var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Register(manager, new ApplicationContext(), "database", async token =>
            {
                using var registration = token.Register(() => cancelled.TrySetResult());
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
                return HealthCheckResult.Healthy();
            }, TimeSpan.FromMilliseconds(100));

            // act
            var healthy = await manager.CheckAsync(TestContext.Current.CancellationToken);
            await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

            // validation
            Assert.False(healthy);
        }

        /// <summary>
        /// Prevents one disconnected probe from cancelling work shared with another caller.
        /// </summary>
        /// <returns>A task that completes after both callers have finished.</returns>
        [Fact]
        public async Task CheckAsync_CallerCancelled_OtherProbeStillSucceeds()
        {
            // arrange
            var (hub, _) = CreateHost();
            using var manager = hub.HealthManager;
            using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            var completion = new TaskCompletionSource<HealthCheckResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var calls = 0;
            Register(manager, new ApplicationContext(), "database", _ =>
            {
                Interlocked.Increment(ref calls);
                entered.TrySetResult();
                return completion.Task;
            });

            // act
            var cancelled = manager.CheckAsync(cancellation.Token);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            var other = manager.CheckAsync(TestContext.Current.CancellationToken);
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelled);
            completion.SetResult(HealthCheckResult.Healthy());
            var healthy = await other;

            // validation
            Assert.True(healthy);
            Assert.Equal(1, calls);
        }

        /// <summary>
        /// Rejects stale success when the host drains or a new dependency appears during a probe.
        /// </summary>
        /// <param name="stopHost">Whether host shutdown or a registry change invalidates the probe.</param>
        /// <returns>A task that completes after the in-flight result is rejected.</returns>
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task CheckAsync_StateChangesDuringProbe_RejectsStaleSuccess(bool stopHost)
        {
            // arrange
            var (hub, server) = CreateHost();
            using var manager = hub.HealthManager;
            var completion = new TaskCompletionSource<HealthCheckResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            Register(manager, new ApplicationContext(), "pending", _ => completion.Task);

            // act
            var probe = manager.CheckAsync(TestContext.Current.CancellationToken);
            if (stopHost)
            {
                SetRunning(server, false);
            }
            else
            {
                Register(manager, new ApplicationContext(), "new", _ => Task.FromResult(HealthCheckResult.Unhealthy()));
            }
            completion.SetResult(HealthCheckResult.Healthy());
            var healthy = await probe;

            // validation
            Assert.False(healthy);
        }

        /// <summary>
        /// Keeps independently bound applications isolated during plugin removal.
        /// </summary>
        /// <returns>A task that completes after registration and removal are validated.</returns>
        [Fact]
        public async Task Register_ApplicationScopeAndDisposal_AreIndependent()
        {
            // arrange
            var (hub, _) = CreateHost();
            using var manager = hub.HealthManager;
            var first = new ApplicationContext { ApplicationId = "first" };
            var second = new ApplicationContext { ApplicationId = "second" };
            using var healthy = Register(manager, first, "database", _ => Task.FromResult(HealthCheckResult.Healthy()));
            var unhealthy = Register(manager, second, "database", _ => Task.FromResult(HealthCheckResult.Unhealthy()));

            // act
            var before = await manager.CheckAsync(TestContext.Current.CancellationToken);
            unhealthy.Dispose();
            unhealthy.Dispose();
            var after = await manager.CheckAsync(TestContext.Current.CancellationToken);

            // validation
            Assert.False(before);
            Assert.True(after);
            Assert.All(manager.HealthChecks, x => Assert.Same(first, x.ApplicationContext));
        }

        /// <summary>
        /// Ensures application unload automatically removes its dependency registrations.
        /// </summary>
        /// <returns>A task that completes after the application removal event is processed.</returns>
        [Fact]
        public async Task RemoveApplication_RemovesOwnedChecks()
        {
            // arrange
            var (hub, _) = CreateHost();
            using var manager = hub.HealthManager;
            ((WebPlugin.PluginManager)hub.PluginManager).Register();
            var application = hub.ApplicationManager.Applications.First();
            Register(manager, application, "database", _ => Task.FromResult(HealthCheckResult.Unhealthy()));

            // act
            var before = await manager.CheckAsync(TestContext.Current.CancellationToken);
            ((ApplicationManager)hub.ApplicationManager).Remove(application.PluginContext);
            var after = await manager.CheckAsync(TestContext.Current.CancellationToken);

            // validation
            Assert.False(before);
            Assert.True(after);
        }

        /// <summary>
        /// Prevents a disposed health registry from becoming a successful empty probe.
        /// </summary>
        /// <returns>A task that completes after disposal behavior is validated.</returns>
        [Fact]
        public async Task Dispose_RejectsProbesAndStopsDiscovery()
        {
            // arrange
            var (hub, _) = CreateHost();
            var manager = hub.HealthManager;

            // act
            manager.Dispose();
            manager.Dispose();
            var healthy = await manager.CheckAsync(TestContext.Current.CancellationToken);

            // validation
            Assert.False(healthy);
            Assert.False(manager.CheckLiveness());
            using var registration = Register(manager, new ApplicationContext(), "database",
                _ => Task.FromResult(HealthCheckResult.Healthy()));
            Assert.Empty(manager.HealthChecks);
        }

        /// <summary>
        /// Isolates dependency aggregation from network binding while retaining real framework managers.
        /// </summary>
        /// <returns>A component hub and its host with completed startup simulated for unit tests.</returns>
        private static (ComponentHub Hub, HttpServer Server) CreateHost()
        {
            var server = new HttpServer(UnitTestFixture.CreateHttpServerContextMock());
            var hub = UnitTestFixture.CreateComponentHubMock(server.HttpServerContext);
            SetRunning(server, true);
            return (hub, server);
        }

        /// <summary>
        /// Simulates lifecycle transitions without opening a listener in manager unit tests.
        /// </summary>
        /// <param name="server">The host whose lifecycle is being simulated.</param>
        /// <param name="running">Whether startup has completed without shutdown beginning.</param>
        private static void SetRunning(HttpServer server, bool running)
        {
            typeof(HttpServer).GetField("_isRunning", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(server, running);
        }
    }
}
