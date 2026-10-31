using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Configuration;
using System.Diagnostics;
using System.Net;
using System.Net.WebSockets;
using System.Reflection;
using System.Text.Json;
using WebExpress.WebCore.Test.Fixture;
using WebExpress.WebCore.Test.WWW.Api._2;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebCore.WebComponent.Model;
using WebExpress.WebCore.WebJob;
using WebExpress.WebCore.WebJob.Model;
using WebExpress.WebCore.WebSetting;
using WebExpress.WebCore.WebSocket;

namespace WebExpress.WebCore.Test.Server
{
    /// <summary>
    /// Verifies shutdown admission, drain ordering, deadlines, and host execution.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestGracefulShutdown
    {
        /// <summary>
        /// Keeps container configuration case insensitive and rejects invalid drain budgets at startup.
        /// </summary>
        /// <param name="mode">The mode supplied by a configuration provider.</param>
        /// <param name="seconds">The configured drain budget.</param>
        /// <param name="valid">Whether startup must accept these settings.</param>
        [Theory]
        [InlineData("graceful", "30", true)]
        [InlineData("GRACEFUL", "1", true)]
        [InlineData("immediate", "30", true)]
        [InlineData("invalid", "30", false)]
        [InlineData("99", "30", false)]
        [InlineData("graceful", "0", false)]
        [InlineData("graceful", "-1", false)]
        [InlineData("graceful", "86401", false)]
        public void Configuration_ValidatesShutdown(string mode, string seconds, bool valid)
        {
            // arrange
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
            {
                ["WebExpress:Shutdown"] = mode,
                ["WebExpress:ShutdownTimeoutSeconds"] = seconds
            }).Build();
            using var configurationOwner = (IDisposable)configuration;

            // act
            var error = Record.Exception(() => configuration.GetServerSettings());

            // validation
            Assert.Equal(valid, error is null);
            if (valid)
            {
                Assert.Equal(Enum.Parse<ShutdownMode>(mode, true), configuration.GetServerSettings().Shutdown);
            }
        }

        /// <summary>
        /// Proves that an admitted HTTP response completes and cleanup waits for background work too.
        /// </summary>
        /// <returns>A task that completes after the live Kestrel round trip and drain assertions.</returns>
        [Fact]
        public async Task Kestrel_Graceful_DrainsRequestsAndWorkersAndRejectsNewWork()
        {
            // arrange
            var server = CreateServer(ShutdownMode.Graceful);
            using var hub = UnitTestFixture.CreateAndRegisterComponentHubMock(server.HttpServerContext);
            hub.SitemapManager.Refresh();
            using var releaseRequest = new ManualResetEventSlim();
            var requestEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var releaseWorker = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var workerEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            TestRestApiB.BeforeGet = () =>
            {
                requestEntered.TrySetResult();
                releaseRequest.Wait(TimeSpan.FromSeconds(10));
            };
            server.HttpServerContext.Lifetime.TryRun(async _ =>
            {
                workerEntered.SetResult();
                await releaseWorker.Task;
            });

            try
            {
                Assert.True(server.Start());
                using var client = CreateClient(server);
                var responseTask = client.GetAsync("/server/appa/api/2/testrestapib", TestContext.Current.CancellationToken);
                await requestEntered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
                await workerEntered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

                // act
                var shutdown = server.StopAsync(TestContext.Current.CancellationToken);
                var repeated = server.StopAsync(TestContext.Current.CancellationToken);

                // validation
                Assert.Same(shutdown, repeated);
                Assert.False(server.IsRunning);
                Assert.False(shutdown.IsCompleted);
                Assert.False(server.HttpServerContext.Lifetime.TryRun(() => throw new Exception("must not execute")));
                var context = UnitTestFixture.CreateHttpContextMock("GET /health HTTP/1.1\r\n\r\n");
                var response = new HttpResponseFeature();
                using var output = new MemoryStream();
                context.Features.Set<IHttpResponseFeature>(response);
                context.Features.Set<IHttpResponseBodyFeature>(new StreamResponseBodyFeature(output));
                await server.ProcessRequestAsync(context);
                Assert.Equal(503, response.StatusCode);
                using var newcomer = CreateClient(server);
                await Assert.ThrowsAsync<HttpRequestException>(() => newcomer.GetAsync("/health", TestContext.Current.CancellationToken));

                releaseRequest.Set();
                using var completed = await responseTask.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
                Assert.Equal(HttpStatusCode.BadRequest, completed.StatusCode);
                Assert.Contains("Not implemented.", await completed.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
                Assert.False(shutdown.IsCompleted);
                releaseWorker.SetResult();
                await shutdown.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            }
            finally
            {
                releaseRequest.Set();
                releaseWorker.TrySetResult();
                TestRestApiB.BeforeGet = null;
                await server.StopAsync(TestContext.Current.CancellationToken);
            }
        }

        /// <summary>
        /// Bounds an uncooperative worker without canceling it prematurely in graceful mode.
        /// </summary>
        /// <param name="mode">The requested shutdown policy.</param>
        /// <returns>A task that completes when the bounded shutdown and remaining worker finish.</returns>
        [Theory]
        [InlineData(ShutdownMode.Graceful)]
        [InlineData(ShutdownMode.Immediate)]
        public async Task StopAsync_DeadlineBoundsUncooperativeWork(ShutdownMode mode)
        {
            // arrange
            var server = CreateServer(mode);
            server.Settings.ShutdownTimeoutSeconds = 1;
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            server.HttpServerContext.Lifetime.TryRun(_ => release.Task);
            var elapsed = Stopwatch.StartNew();

            try
            {
                // act
                await server.StopAsync(TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

                // validation
                Assert.False(release.Task.IsCompleted);
                Assert.True(elapsed.Elapsed < TimeSpan.FromSeconds(4));
                if (mode == ShutdownMode.Graceful)
                {
                    Assert.True(elapsed.Elapsed >= TimeSpan.FromMilliseconds(850));
                    Assert.Contains(server.HttpServerContext.Log.GetRecentEntries(), x => x.Message.Contains("deadline reached"));
                }
            }
            finally
            {
                release.TrySetResult();
                await server.HttpServerContext.Lifetime.StopAsync(TestContext.Current.CancellationToken);
            }
        }

        /// <summary>
        /// Ensures a request that ignores cancellation cannot keep a container draining indefinitely.
        /// </summary>
        /// <returns>A task that completes after the transport is aborted and the handler is released.</returns>
        [Fact]
        public async Task Kestrel_DeadlineAbortsBlockedRequest()
        {
            // arrange
            var server = CreateServer(ShutdownMode.Graceful);
            server.Settings.ShutdownTimeoutSeconds = 1;
            using var hub = UnitTestFixture.CreateAndRegisterComponentHubMock(server.HttpServerContext);
            hub.SitemapManager.Refresh();
            using var release = new ManualResetEventSlim();
            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var returned = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            TestRestApiB.BeforeGet = () =>
            {
                entered.TrySetResult();
                release.Wait(TimeSpan.FromSeconds(10));
                returned.TrySetResult();
            };

            try
            {
                Assert.True(server.Start());
                using var client = CreateClient(server);
                var request = client.GetAsync("/server/appa/api/2/testrestapib", TestContext.Current.CancellationToken);
                await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

                // act
                await server.StopAsync(TestContext.Current.CancellationToken)
                    .WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

                // validation
                Assert.False(returned.Task.IsCompleted);
                await Assert.ThrowsAsync<HttpRequestException>(() => request);
                Assert.Contains(server.HttpServerContext.Log.GetRecentEntries(), x => x.Message.Contains("deadline reached"));
            }
            finally
            {
                release.Set();
                TestRestApiB.BeforeGet = null;
                await server.StopAsync(TestContext.Current.CancellationToken);
                if (entered.Task.IsCompleted)
                {
                    await returned.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
                }
            }
        }

        /// <summary>
        /// Keeps failed workers and stop callbacks from skipping another worker's drain.
        /// </summary>
        /// <returns>A task that completes after every admitted worker has returned.</returns>
        [Fact]
        public async Task Lifetime_FailureDoesNotSkipDrain()
        {
            // arrange
            var context = UnitTestFixture.CreateHttpServerContextMock();
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var registration = context.Lifetime.Stopping.Register(() => throw new InvalidOperationException("callback failure"));
            context.Lifetime.TryRun(() => throw new InvalidOperationException("worker failure"));
            context.Lifetime.TryRun(_ => release.Task);

            // act
            var drain = context.Lifetime.StopAsync(TestContext.Current.CancellationToken);

            // validation
            Assert.True(context.Lifetime.Stopping.IsCancellationRequested);
            Assert.False(drain.IsCompleted);
            release.SetResult();
            await drain.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            Assert.Contains(context.Log.GetRecentEntries(), x => x.Message.Contains("worker failure"));
            Assert.Contains(context.Log.GetRecentEntries(), x => x.Message.Contains("callback failure"));
        }

        /// <summary>
        /// Ensures idle upgraded connections receive a close frame without consuming the drain budget.
        /// </summary>
        /// <returns>A task that completes after a real WebSocket receives the shutdown close frame.</returns>
        [Fact]
        public async Task Kestrel_ShutdownClosesIdleWebSocket()
        {
            // arrange
            var server = CreateServer(ShutdownMode.Graceful);
            using var hub = UnitTestFixture.CreateAndRegisterComponentHubMock(server.HttpServerContext);
            var application = hub.ApplicationManager.GetApplications(typeof(TestApplicationA)).First();
            var socket = (SocketContext)hub.SocketManager.GetSockets<TestSocketA>(application).First();
            socket.SupportedSubProtocol = "wxmsg";
            hub.SitemapManager.Refresh();
            using var client = new ClientWebSocket();
            client.Options.AddSubProtocol("wxmsg");
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(5));

            try
            {
                Assert.True(server.Start());
                using var http = CreateClient(server);
                var target = new UriBuilder(http.BaseAddress) { Scheme = "ws", Path = socket.Route.ToString() }.Uri;
                await client.ConnectAsync(target, timeout.Token);

                // act
                var shutdown = server.StopAsync(timeout.Token);
                var result = await client.ReceiveAsync(new ArraySegment<byte>(new byte[128]), timeout.Token);
                await shutdown.WaitAsync(timeout.Token);

                // validation
                Assert.Equal(WebSocketMessageType.Close, result.MessageType);
                Assert.Equal(WebSocketCloseStatus.NormalClosure, result.CloseStatus);
                Assert.Equal("server shutdown", result.CloseStatusDescription);
            }
            finally
            {
                client.Abort();
                await server.StopAsync(TestContext.Current.CancellationToken);
            }
        }

        /// <summary>
        /// Exercises the scheduler and task manager instead of only the shared lifetime abstraction.
        /// </summary>
        /// <returns>A task that completes once admitted jobs and ad-hoc tasks finish.</returns>
        [Fact]
        public async Task Managers_DrainScheduledAndAdHocWorkBeforeDisposal()
        {
            // arrange
            var context = UnitTestFixture.CreateHttpServerContextMock();
            using var hub = UnitTestFixture.CreateComponentHubMock(context);
            var manager = (JobManager)hub.JobManager;
            using var release = new ManualResetEventSlim();
            var jobEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var taskEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var job = new BlockingJob(jobEntered, release);
            var item = new ScheduleItem(hub, context, null, null, new JobContext { Cron = new Cron(context) }, typeof(Job))
            {
                Instance = job
            };
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var schedules = (List<ScheduleItem>)typeof(JobManager).GetField("_dynamicScheduleList", flags).GetValue(manager);
            schedules.Add(item);
            var clock = (Clock)typeof(JobManager).GetField("_clock", flags).GetValue(manager);
            var previousMinute = DateTime.Now.AddMinutes(-1);
            typeof(Clock).GetField("_dateTime", flags).SetValue(clock,
                new DateTime(previousMinute.Year, previousMinute.Month, previousMinute.Day, previousMinute.Hour, previousMinute.Minute, 0));
            var task = hub.TaskManager.CreateTask("shutdown-test", (_, _) =>
            {
                taskEntered.TrySetResult();
                release.Wait(TimeSpan.FromSeconds(10));
            });

            try
            {
                manager.Execute();
                task.Run();
                await jobEntered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
                await taskEntered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

                // act
                var drain = context.Lifetime.StopAsync(TestContext.Current.CancellationToken);

                // validation
                Assert.False(drain.IsCompleted);
                Assert.Equal(0, job.Disposals);
                release.Set();
                await drain.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
                hub.Dispose();
                hub.Dispose();
                Assert.Equal(1, job.Calls);
                Assert.Equal(1, job.Disposals);
            }
            finally
            {
                release.Set();
                await context.Lifetime.StopAsync(TestContext.Current.CancellationToken);
            }
        }

        /// <summary>
        /// Ensures the real host returns once, drains work, and continues after an exit callback fails.
        /// </summary>
        /// <returns>A task that completes after the host execution thread has exited normally.</returns>
        [Fact]
        public async Task Execution_RequestShutdownReturnsAndReleasesComponentsOnce()
        {
            // arrange
            var directory = Path.Combine(Path.GetTempPath(), "webexpress-shutdown-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            var settingsFile = Path.Combine(directory, "webexpress.settings.json");
            File.WriteAllText(settingsFile, JsonSerializer.Serialize(new
            {
                WebExpress = new
                {
                    Shutdown = "graceful",
                    ShutdownTimeoutSeconds = 5,
                    Endpoints = new[] { new { Uri = "http://127.0.0.1:0/" } },
                    PackagePath = Path.Combine(directory, "packages"),
                    AssetPath = Path.Combine(directory, "assets"),
                    DataPath = Path.Combine(directory, "data"),
                    Culture = "en-US"
                }
            }));
            var app = new WebEx();
            var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var workerFinished = false;
            var exits = 0;
            var disposed = 0;
            app.Start += (_, _) =>
            {
                var hub = (ComponentHub)WebEx.ComponentHub;
                var dictionary = (ComponentDictionary)typeof(ComponentHub)
                    .GetField("_dictionary", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(hub);
                dictionary[new WebPlugin.PluginContext()] = [new ComponentItem
                {
                    ComponentInstance = new CleanupProbe(() =>
                    {
                        Assert.True(workerFinished);
                        disposed++;
                    })
                }];
                var serverContext = (IHttpServerContext)typeof(ComponentHub)
                    .GetField("_httpServerContext", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(hub);
                serverContext.Lifetime.TryRun(async stopping =>
                {
                    try
                    {
                        await Task.Delay(Timeout.InfiniteTimeSpan, stopping);
                    }
                    catch (OperationCanceledException)
                    {
                        workerFinished = true;
                    }
                });
                started.SetResult();
            };
            app.Exit += (_, _) => throw new InvalidOperationException("exit callback failure");
            app.Exit += (_, _) => exits++;
            var execution = Task.Run(() => app.Execution(["-config", settingsFile]), TestContext.Current.CancellationToken);

            try
            {
                await started.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

                // act
                app.RequestShutdown();
                app.RequestShutdown();
                var code = await execution.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

                // validation
                Assert.Equal(0, code);
                Assert.True(workerFinished);
                Assert.Equal(1, exits);
                Assert.Equal(1, disposed);
            }
            finally
            {
                app.RequestShutdown();
                await execution.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
                Directory.Delete(directory, true);
            }
        }

        /// <summary>
        /// Records release ordering without adding a discoverable production component.
        /// </summary>
        /// <param name="dispose">The ordering assertion executed during cleanup.</param>
        private sealed class CleanupProbe(Action dispose) : IComponentManager
        {
            /// <summary>
            /// Runs the release assertion after admitted work has completed.
            /// </summary>
            public void Dispose()
            {
                dispose();
            }
        }

        /// <summary>
        /// Models a transaction that must return before its owning job is disposed.
        /// </summary>
        /// <param name="entered">The signal raised after execution begins.</param>
        /// <param name="release">The signal allowing the transaction to finish.</param>
        private sealed class BlockingJob(TaskCompletionSource entered, ManualResetEventSlim release) : Job
        {
            /// <summary>
            /// Gets the number of executions to detect duplicate dynamic scheduling.
            /// </summary>
            public int Calls { get; private set; }

            /// <summary>
            /// Gets the number of releases to detect duplicate component cleanup.
            /// </summary>
            public int Disposals { get; private set; }

            /// <summary>
            /// Keeps a transaction active until the test permits it to finish.
            /// </summary>
            public override void Process()
            {
                Calls++;
                entered.TrySetResult();
                release.Wait(TimeSpan.FromSeconds(10));
            }

            /// <summary>
            /// Records the release of resources owned by the completed transaction.
            /// </summary>
            public override void Dispose()
            {
                Disposals++;
            }
        }

        /// <summary>
        /// Creates an isolated listener with a bounded shutdown policy.
        /// </summary>
        /// <param name="mode">The shutdown policy under test.</param>
        /// <returns>A server that binds an ephemeral loopback port when started.</returns>
        private static HttpServer CreateServer(ShutdownMode mode)
        {
            return new HttpServer(UnitTestFixture.CreateHttpServerContextMock())
            {
                Settings = new HttpServerSettings
                {
                    Endpoints = [new EndpointSettings { Uri = "http://127.0.0.1:0/" }],
                    Shutdown = mode,
                    ShutdownTimeoutSeconds = 10
                }
            };
        }

        /// <summary>
        /// Connects to the actual ephemeral listener rather than guessing an available port.
        /// </summary>
        /// <param name="server">The server whose listener has started.</param>
        /// <returns>A client with a bounded request timeout.</returns>
        private static HttpClient CreateClient(HttpServer server)
        {
            var kestrel = (IServer)typeof(HttpServer).GetProperty("Kestrel", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(server);
            return new HttpClient
            {
                BaseAddress = new Uri(kestrel.Features.Get<IServerAddressesFeature>().Addresses.Single()),
                Timeout = TimeSpan.FromSeconds(5)
            };
        }
    }
}
