using System.Reflection;
using WebExpress.WebCore.Test.Data;
using WebExpress.WebCore.Test.Fixture;
using WebExpress.WebCore.WebApplication;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebCore.WebMetrics;
using static WebExpress.WebCore.Test.Fixture.MetricTestFixture;

namespace WebExpress.WebCore.Test.Manager
{
    /// <summary>
    /// Verifies that framework series, shared instruments and discovered components merge into one valid scrape.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestMetricsManager
    {
        /// <summary>
        /// Discovers components through real plugin events and labels each binding with its application.
        /// </summary>
        /// <returns>A task that completes after discovery, removal and rebinding have been validated.</returns>
        [Fact]
        public async Task PluginAndApplicationEvents_DiscoverComponentsAutomatically()
        {
            // arrange
            var hub = UnitTestFixture.CreateComponentHubMock();
            using var manager = hub.MetricsManager;
            var additions = new List<IMetricContext>();
            var removals = new List<IMetricContext>();
            manager.AddMetric += (_, context) => additions.Add(context);
            manager.RemoveMetric += (_, context) => removals.Add(context);

            // act
            ((WebPlugin.PluginManager)hub.PluginManager).Register();
            var plugin = hub.PluginManager.GetPlugin(typeof(TestPlugin));
            var first = manager.Metrics.ToArray();
            var scrape = await ScrapeAsync(manager);
            ((ApplicationManager)hub.ApplicationManager).Remove(plugin);
            var empty = manager.Metrics.ToArray();
            typeof(ApplicationManager).GetMethod("Register", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(hub.ApplicationManager, [plugin]);
            var rebound = manager.Metrics.ToArray();

            // validation
            Assert.Equal(3, first.Length);
            Assert.Equal(3, rebound.Length);
            Assert.Equal(6, additions.Count);
            Assert.Equal(3, removals.Count);
            Assert.Empty(empty);
            Assert.All(first, x => Assert.Equal(typeof(TestMetric).FullName.ToLowerInvariant(), x.MetricId.ToString()));
            foreach (var context in first)
            {
                Assert.Contains($"test_metric_value{{application=\"{context.ApplicationContext.ApplicationId}\"}} 1", scrape);
                Assert.Contains($"webexpress_metric_collector_up{{application=\"{context.ApplicationContext.ApplicationId}\"," +
                    $"collector=\"{context.MetricId}\"}} 1", scrape);
            }
            Assert.Contains($"webexpress_applications {hub.ApplicationManager.Applications.Count()}", scrape);
        }

        /// <summary>
        /// Avoids duplicate bindings and keeps a separate injected instance per application.
        /// </summary>
        /// <returns>A task that completes after discovery, activation and disposal have been validated.</returns>
        [Fact]
        public async Task Discovery_IsIdempotentAndInstancesBelongToApplications()
        {
            // arrange
            var hub = UnitTestFixture.CreateComponentHubMock();
            using var manager = hub.MetricsManager;
            var plugin = new MetricTestPluginContext("plugin", typeof(TestMetric), typeof(UnsealedMetric),
                typeof(InvalidTimeoutMetric<>), typeof(IMetric));
            var first = new ApplicationContext { ApplicationId = "first" };
            var second = new ApplicationContext { ApplicationId = "second" };

            // act
            ((MetricsManager)manager).Register(plugin, [first, second, first]);
            ((MetricsManager)manager).Register(plugin, [first, second]);
            var bindings = manager.Metrics.ToArray();
            var firstBindings = manager.GetMetrics(first).ToArray();
            await ScrapeAsync(manager);
            var scrape = await ScrapeAsync(manager);
            ((MetricsManager)manager).Remove(plugin);

            // validation
            Assert.Equal(2, bindings.Length);
            Assert.Same(first, Assert.Single(firstBindings).ApplicationContext);
            Assert.Equal(TimeSpan.FromSeconds(2), bindings[0].Timeout);
            Assert.Equal(2, plugin.Created);
            Assert.Equal(2, plugin.Disposed);
            Assert.Empty(manager.Metrics);
            Assert.DoesNotContain("unsealed_metric", scrape);
        }

        /// <summary>
        /// Keeps a failing, slow or misconfigured component from costing the other sources of a scrape,
        /// and reports it through the collector status series instead.
        /// </summary>
        /// <param name="failure">The failure mode of the component.</param>
        /// <returns>A task that completes after the scrape has been validated.</returns>
        [Theory]
        [InlineData("throw")]
        [InlineData("faulted")]
        [InlineData("null-task")]
        [InlineData("timeout")]
        [InlineData("invalid-name")]
        [InlineData("reserved-label")]
        [InlineData("duplicate-series")]
        [InlineData("constructor")]
        public async Task CollectAsync_ComponentFails_OnlyItsSeriesAreLost(string failure)
        {
            // arrange
            var hub = UnitTestFixture.CreateComponentHubMock();
            using var manager = hub.MetricsManager;
            var application = new ApplicationContext { ApplicationId = "app" };
            Register(manager, application, "healthy", (collector, _) =>
            {
                collector.Gauge("healthy_value", "A value of a working component.", 7);
                return Task.CompletedTask;
            });
            using var failing = Register(manager, new ApplicationContext { ApplicationId = "broken" }, "failing", async (collector, token) =>
            {
                collector.Gauge("partial_value", "A value reported before the failure.", 1);
                switch (failure)
                {
                    case "throw": throw new InvalidOperationException("secret-diagnostic");
                    case "faulted": await Task.FromException(new InvalidOperationException("secret-diagnostic")); break;
                    case "timeout": await Task.Delay(Timeout.InfiniteTimeSpan, token); break;
                    case "invalid-name": collector.Gauge("invalid-name", "", 1); break;
                    case "reserved-label": collector.Gauge("labelled", "", 1, new MetricLabel("application", "spoofed")); break;
                    case "duplicate-series": collector.Gauge("partial_value", "", 2); break;
                }
            }, TimeSpan.FromMilliseconds(100));
            failing.Plugin.FailConstruction = failure == "constructor";
            if (failure == "null-task")
            {
                failing.Plugin.Collect = (_, _) => null;
            }

            // act
            var scrape = await ScrapeAsync(manager);

            // validation
            Assert.Contains("healthy_value{application=\"app\"} 7", scrape);
            Assert.DoesNotContain("partial_value", scrape);
            Assert.DoesNotContain("spoofed", scrape);
            Assert.DoesNotContain("secret-diagnostic", scrape);
            Assert.Matches("webexpress_metric_collector_up\\{application=\"app\",collector=\"[^\"]+\"\\} 1", scrape);
            Assert.Matches("webexpress_metric_collector_up\\{application=\"broken\",collector=\"[^\"]+\"\\} 0", scrape);
        }

        /// <summary>
        /// Applies declarative budgets while treating invalid declarations as a failed collector.
        /// </summary>
        /// <param name="invalid">Whether the component declares a non-positive timeout.</param>
        /// <returns>A task that completes after the component budget has been validated.</returns>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Discovery_TimeoutAttribute_GovernsCollection(bool invalid)
        {
            // arrange
            var hub = UnitTestFixture.CreateComponentHubMock();
            using var manager = hub.MetricsManager;
            var plugin = new MetricTestPluginContext("plugin", invalid ? typeof(InvalidTimeoutMetric<int>) : typeof(TimedMetric<int>));

            // act
            ((MetricsManager)manager).Register(plugin, [new ApplicationContext { ApplicationId = "app" }]);
            var scrape = await ScrapeAsync(manager);

            // validation
            Assert.Equal(invalid ? TimeSpan.FromSeconds(2) : TimeSpan.FromMilliseconds(25), Assert.Single(manager.Metrics).Timeout);
            Assert.Matches("webexpress_metric_collector_up\\{application=\"app\",collector=\"[^\"]+\"\\} 0", scrape);
        }

        /// <summary>
        /// Shares one collection between concurrent scrapes, so replicas of the scraper do not multiply the work.
        /// </summary>
        /// <returns>A task that completes after both scrapes have finished.</returns>
        [Fact]
        public async Task CollectAsync_ConcurrentScrapes_ShareOneCollection()
        {
            // arrange
            var hub = UnitTestFixture.CreateComponentHubMock();
            using var manager = hub.MetricsManager;
            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var calls = 0;
            Register(manager, new ApplicationContext { ApplicationId = "app" }, "shared", async (collector, _) =>
            {
                Interlocked.Increment(ref calls);
                entered.TrySetResult();
                await release.Task;
                collector.Gauge("shared_value", "", 3);
            });

            // act
            var first = ScrapeAsync(manager);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            var second = ScrapeAsync(manager);
            release.SetResult();
            var scrapes = await Task.WhenAll(first, second);

            // validation
            Assert.Equal(1, calls);
            Assert.All(scrapes, x => Assert.Contains("shared_value{application=\"app\"} 3", x));
        }

        /// <summary>
        /// Delays resource disposal until a detached component's active collection finishes.
        /// </summary>
        /// <returns>A task that completes after deferred cleanup has been validated.</returns>
        [Fact]
        public async Task RemovePlugin_InFlightCollection_DefersInstanceDisposal()
        {
            // arrange
            var hub = UnitTestFixture.CreateComponentHubMock();
            using var manager = hub.MetricsManager;
            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var registration = Register(manager, new ApplicationContext(), "plugin", async (_, _) =>
            {
                entered.TrySetResult();
                await release.Task;
            });

            // act
            var scrape = manager.CollectAsync(TestContext.Current.CancellationToken);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            registration.Dispose();
            var disposedWhileRunning = registration.Plugin.Disposed;
            release.SetResult();
            await scrape;
            await registration.Plugin.DisposedSignal.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

            // validation
            Assert.Equal(0, disposedWhileRunning);
            Assert.Equal(1, registration.Plugin.Disposed);
            Assert.Empty(manager.Metrics);
        }

        /// <summary>
        /// Shares instruments by name and refuses a second declaration that would make the series ambiguous.
        /// </summary>
        [Fact]
        public void CreateInstrument_SharesByNameAndRejectsIncompatibleDeclarations()
        {
            // arrange
            var hub = UnitTestFixture.CreateComponentHubMock();
            using var manager = hub.MetricsManager;

            // act
            var counter = manager.CreateCounter("ldap_requests_total", "LDAP requests.", "result");
            var same = manager.CreateCounter("ldap_requests_total", "Other help is ignored.", "result");
            var histogram = manager.CreateHistogram("ldap_request_duration_seconds", "LDAP latency.", [0.1, 1]);
            var framework = manager.CreateCounter("webexpress_identity_logins_total", "", "result");

            // validation
            Assert.Same(counter, same);
            Assert.Same(histogram, manager.CreateHistogram("ldap_request_duration_seconds", "", [0.1, 1]));
            Assert.Same(((MetricsManager)manager).Framework.Logins, framework);
            Assert.Throws<InvalidOperationException>(() => manager.CreateGauge("ldap_requests_total", ""));
            Assert.Throws<InvalidOperationException>(() => manager.CreateCounter("ldap_requests_total", "", "server"));
            Assert.Throws<InvalidOperationException>(() => manager.CreateHistogram("ldap_request_duration_seconds", "", [0.5]));
            Assert.Throws<ArgumentException>(() => manager.CreateCounter("ldap requests", ""));
            Assert.Contains(counter, manager.Instruments);
        }

        /// <summary>
        /// Exports shared instruments and drops a component series that contradicts one, instead of
        /// letting the conflict invalidate the whole scrape.
        /// </summary>
        /// <returns>A task that completes after the scrape has been validated.</returns>
        [Fact]
        public async Task CollectAsync_TypeConflictBetweenSources_KeepsFirstAndLogs()
        {
            // arrange
            var hub = UnitTestFixture.CreateComponentHubMock();
            using var manager = hub.MetricsManager;
            manager.CreateCounter("orders_total", "Orders placed.", "channel").Increment("web");
            Register(manager, new ApplicationContext { ApplicationId = "shop" }, "shop", (collector, _) =>
            {
                collector.Gauge("orders_total", "Contradicting type.", 5);
                collector.Gauge("cart_items", "Items in carts.", 2);
                return Task.CompletedTask;
            });

            // act
            var scrape = await ScrapeAsync(manager);

            // validation
            Assert.Contains("# TYPE orders_total counter", scrape);
            Assert.Contains("orders_total{channel=\"web\"} 1", scrape);
            Assert.DoesNotContain("orders_total{application=\"shop\"}", scrape);
            Assert.Contains("cart_items{application=\"shop\"} 2", scrape);
            Assert.Single(scrape.Split('\n'), x => x == "# TYPE orders_total counter");
        }

        /// <summary>
        /// Reports the process, runtime and framework baseline without any application component.
        /// </summary>
        /// <returns>A task that completes after the baseline has been validated.</returns>
        [Fact]
        public async Task CollectAsync_WithoutComponents_ReportsFrameworkBaseline()
        {
            // arrange
            var hub = UnitTestFixture.CreateComponentHubMock();
            using var manager = hub.MetricsManager;

            // act
            var scrape = await ScrapeAsync(manager);

            // validation
            foreach (var family in new[]
            {
                "webexpress_info gauge", "webexpress_http_request_duration_seconds histogram",
                "webexpress_http_requests_in_flight gauge", "webexpress_identity_logouts_total counter",
                "webexpress_identity_active_users gauge", "webexpress_sessions gauge", "process_cpu_seconds_total counter",
                "process_resident_memory_bytes gauge", "process_start_time_seconds gauge", "dotnet_total_memory_bytes gauge",
                "dotnet_collection_count_total counter", "dotnet_threadpool_threads gauge"
            })
            {
                Assert.Contains("# TYPE " + family + "\n", scrape);
            }

            Assert.Contains("webexpress_http_request_duration_seconds_bucket{le=\"+Inf\"} 0", scrape);
            Assert.Contains("webexpress_identity_logouts_total 0", scrape);
            Assert.DoesNotContain("webexpress_metric_collector_up", scrape);
        }

        /// <summary>
        /// Reports how far the clock of each cluster instance that sent a message runs ahead, so
        /// an alert can catch clocks drifting apart before deadlines and messages suffer.
        /// </summary>
        /// <returns>A task that completes after the scrape has been validated.</returns>
        [Fact]
        public async Task CollectAsync_ClusterPeer_ReportsClockSkew()
        {
            // arrange
            var hub = UnitTestFixture.CreateComponentHubMock();
            using var manager = hub.MetricsManager;
            ((WebCluster.ClusterManager)hub.ClusterManager).MeasureClock(new WebCluster.ClusterMessage("t", "pod-2", [], DateTimeOffset.UtcNow.AddSeconds(30)));

            // act
            var scrape = await ScrapeAsync(manager);

            // validation
            Assert.Contains("# TYPE webexpress_cluster_clock_skew_seconds gauge\n", scrape);
            Assert.Contains("webexpress_cluster_clock_skew_seconds{peer=\"pod-2\"} 29.", scrape);
        }

        /// <summary>
        /// Counts identities seen within the window once each and forgets those outside it.
        /// </summary>
        /// <returns>A task that completes after the active user gauge has been validated.</returns>
        [Fact]
        public async Task ActiveUsers_CountsDistinctIdentitiesWithinWindow()
        {
            // arrange
            var hub = UnitTestFixture.CreateComponentHubMock();
            using var manager = hub.MetricsManager;
            var framework = ((MetricsManager)manager).Framework;
            var alice = Guid.NewGuid();
            var stale = Guid.NewGuid();

            // act
            framework.RecordActiveUser(alice);
            framework.RecordActiveUser(alice);
            framework.RecordActiveUser(Guid.NewGuid());
            var activeUsers = (System.Collections.Concurrent.ConcurrentDictionary<Guid, long>)typeof(WebMetrics.Model.FrameworkMetrics)
                .GetField("_activeUsers", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(framework);
            activeUsers[stale] = Environment.TickCount64 - (long)TimeSpan.FromHours(1).TotalMilliseconds;
            var scrape = await ScrapeAsync(manager);

            // validation
            Assert.Contains("webexpress_identity_active_users 2", scrape);
            Assert.False(activeUsers.ContainsKey(stale));
        }

        /// <summary>
        /// Rejects scrapes of a disposed registry instead of reporting an empty, seemingly healthy process.
        /// </summary>
        /// <returns>A task that completes after disposal behavior has been validated.</returns>
        [Fact]
        public async Task Dispose_RejectsScrapesAndStopsDiscovery()
        {
            // arrange
            var hub = UnitTestFixture.CreateComponentHubMock();
            var manager = hub.MetricsManager;

            // act
            manager.Dispose();
            manager.Dispose();

            // validation
            await Assert.ThrowsAsync<ObjectDisposedException>(() => manager.CollectAsync(TestContext.Current.CancellationToken));
            using var registration = Register(manager, new ApplicationContext(), "late", (_, _) => Task.CompletedTask);
            Assert.Empty(manager.Metrics);
            Assert.Contains(manager, hub.Managers);
        }
    }
}
