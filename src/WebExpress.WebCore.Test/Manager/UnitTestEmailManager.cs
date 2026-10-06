using Microsoft.Extensions.Configuration;
using MimeKit;
using System.Net;
using System.Net.Sockets;
using System.Text;
using WebExpress.WebCore.Test.Fixture;
using WebExpress.WebCore.WebCluster;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebCore.WebEmail;
using WebExpress.WebCore.WebSetting;

namespace WebExpress.WebCore.Test.Manager
{
    /// <summary>
    /// Verifies shared delivery policy and actual SMTP envelopes without contacting external mail systems.
    /// </summary>
    [Collection("NonParallelTests")]
    public sealed class UnitTestEmailManager
    {
        /// <summary>
        /// Verifies that every hub exposes the manager and leaves sending disabled unless configured.
        /// </summary>
        [Fact]
        public async Task HubOwnsManagerAndDisablesUnconfiguredSending()
        {
            // arrange
            using var hub = UnitTestFixture.CreateComponentHubMock();

            // act
            var error = await Assert.ThrowsAsync<EmailException>(() => hub.EmailManager.SendAsync("app", Message(), cancellationToken: TestContext.Current.CancellationToken));

            // validation
            Assert.Contains(hub.EmailManager, hub.Managers);
            Assert.Equal(EmailError.Disabled, error.Error);
        }

        /// <summary>
        /// Keeps diagnostics current when providers load or unload without leaking credential configuration.
        /// </summary>
        [Fact]
        public void StatusTracksProviderLifecycleWithoutMutatingEarlierSnapshots()
        {
            // arrange
            using var hub = CreateHub(new()
            {
                ["WebExpress:Email:Profiles:default:Options:apiKey"] = "secret-api-key",
                ["WebExpress:Email:Profiles:default:UserName"] = "private-account",
                ["WebExpress:Email:Profiles:default:Password"] = "secret-password"
            });
            var provider = new TestProvider();
            var before = hub.EmailManager.GetStatus();

            // act
            hub.EmailManager.RegisterProvider(provider);
            var registered = hub.EmailManager.GetStatus();
            hub.EmailManager.UnregisterProvider(provider);
            var removed = hub.EmailManager.GetStatus();
            var json = System.Text.Json.JsonSerializer.Serialize(registered);

            // validation
            Assert.False(Assert.Single(before.Profiles).ProviderRegistered);
            Assert.True(Assert.Single(registered.Profiles).ProviderRegistered);
            Assert.False(Assert.Single(removed.Profiles).ProviderRegistered);
            Assert.DoesNotContain("secret-api-key", json);
            Assert.DoesNotContain("private-account", json);
            Assert.DoesNotContain("secret-password", json);
            Assert.DoesNotContain("Password", json);
            Assert.DoesNotContain("Options", json);
        }

        /// <summary>
        /// Verifies both body alternatives, copied binary data and provider-specific profile selection.
        /// </summary>
        [Fact]
        public async Task SnapshotPreservesBodiesRecipientsAndAttachmentBytes()
        {
            // arrange
            using var hub = CreateHub(new() { ["WebExpress:Email:Profiles:default:Options:region"] = "eu" });
            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            byte[] received = null;
            var provider = new TestProvider(async (message, profile, token) =>
            {
                entered.SetResult();
                await release.Task.WaitAsync(token);
                Assert.Equal("Plain München", message.TextBody);
                Assert.Equal("<p>HTML München</p>", message.HtmlBody);
                Assert.Single(message.To);
                Assert.Single(message.Cc);
                Assert.Single(message.Bcc);
                Assert.Equal("reply@example.org", message.ReplyTo.Mailboxes.Single().Address);
                Assert.Equal("eu", profile.Options["region"]);
                using var output = new MemoryStream();
                ((MimePart)message.Attachments.Single()).Content.DecodeTo(output, TestContext.Current.CancellationToken);
                received = output.ToArray();
            });
            hub.EmailManager.RegisterProvider(provider);
            byte[] bytes = [0, 128, 255];
            var input = new EmailMessage
            {
                To = ["receiver@example.org"], Cc = ["copy@example.org"], Bcc = ["hidden@example.org"],
                ReplyTo = "reply@example.org", Subject = "München", TextBody = "Plain München",
                HtmlBody = "<p>HTML München</p>",
                Attachments = [new() { FileName = "report.bin", Content = bytes }]
            };

            // act
            var sending = hub.EmailManager.SendAsync("app", input, "DEFAULT", cancellationToken: TestContext.Current.CancellationToken);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            bytes[0] = 99;
            release.SetResult();
            var result = await sending;

            // validation
            Assert.Equal(EmailSendResult.Accepted, result);
            Assert.Equal(new byte[] { 0, 128, 255 }, received);
        }

        /// <summary>
        /// Verifies that malformed content is rejected before creating a deduplication claim.
        /// </summary>
        /// <param name="kind">The invalid input category.</param>
        [Theory]
        [InlineData("recipient")]
        [InlineData("header")]
        [InlineData("body")]
        [InlineData("attachment")]
        [InlineData("size")]
        [InlineData("sender")]
        public async Task InvalidInputDoesNotClaimDelivery(string kind)
        {
            // arrange
            using var hub = CreateHub(new() { ["WebExpress:Email:MaxAttachmentBytes"] = "2" });
            var provider = new TestProvider();
            hub.EmailManager.RegisterProvider(provider);
            var invalid = new EmailMessage
            {
                DeliveryId = "business-event",
                To = kind == "recipient" ? ["bad\r\nBcc: stolen@example.org"] : ["receiver@example.org"],
                From = kind == "sender" ? "not-an-address" : null,
                Subject = kind == "header" ? "subject\r\nX-Injected: yes" : "subject",
                TextBody = kind == "body" ? null : "body",
                Attachments = kind == "attachment" ? [new() { FileName = "../file", Content = [1] }]
                    : kind == "size" ? [new() { FileName = "file", Content = [1, 2, 3] }] : []
            };

            // act
            var error = await Assert.ThrowsAsync<EmailException>(() => hub.EmailManager.SendAsync("app", invalid, cancellationToken: TestContext.Current.CancellationToken));
            var corrected = await hub.EmailManager.SendAsync("app", Message("business-event"), cancellationToken: TestContext.Current.CancellationToken);

            // validation
            Assert.Equal(EmailError.InvalidMessage, error.Error);
            Assert.Equal(EmailSendResult.Accepted, corrected);
            Assert.Equal(1, provider.Calls);
        }

        /// <summary>
        /// Verifies that shared atomic claims suppress concurrent replicas and survive manager recreation.
        /// </summary>
        [Fact]
        public async Task ReplicasAndRestartShareClaimsButApplicationsRemainIndependent()
        {
            // arrange
            var directory = Path.Combine(Path.GetTempPath(), "wx-email-" + Guid.NewGuid().ToString("N"));
            try
            {
                using var first = CreateHub();
                using var second = CreateHub();
                first.ClusterManager.UseStore(new FileClusterStore(directory));
                second.ClusterManager.UseStore(new FileClusterStore(directory));
                var provider = new TestProvider();
                first.EmailManager.RegisterProvider(provider);
                second.EmailManager.RegisterProvider(provider);

                // act
                var attempts = await Task.WhenAll(Enumerable.Range(0, 20).Select(index =>
                    (index % 2 == 0 ? first : second).EmailManager.SendAsync("app", Message("event"), cancellationToken: TestContext.Current.CancellationToken)));
                using var restarted = CreateHub();
                restarted.ClusterManager.UseStore(new FileClusterStore(directory));
                restarted.EmailManager.RegisterProvider(provider);
                var duplicate = await restarted.EmailManager.SendAsync("app", Message("event"), cancellationToken: TestContext.Current.CancellationToken);
                var independent = await restarted.EmailManager.SendAsync("other-app", Message("event"), cancellationToken: TestContext.Current.CancellationToken);

                // validation
                Assert.Single(attempts, result => result == EmailSendResult.Accepted);
                Assert.Equal(19, attempts.Count(result => result == EmailSendResult.AlreadyAttempted));
                Assert.Equal(EmailSendResult.AlreadyAttempted, duplicate);
                Assert.Equal(EmailSendResult.Accepted, independent);
                Assert.Equal(2, provider.Calls);
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        /// <summary>
        /// Verifies that uncertain provider failures stay claimed and sensitive exception text stays out of logs.
        /// </summary>
        [Fact]
        public async Task FailureIsConsistentAndNeverAutomaticallyRetried()
        {
            // arrange
            var configuration = Configuration();
            var context = UnitTestFixture.CreateHttpServerContextMock(configuration: configuration);
            using var hub = UnitTestFixture.CreateComponentHubMock(context);
            var provider = new TestProvider((_, _, _) => throw new IOException("password=secret recipient@example.org"));
            hub.EmailManager.RegisterProvider(provider);

            // act
            var error = await Assert.ThrowsAsync<EmailException>(() => hub.EmailManager.SendAsync("app", Message("event"), cancellationToken: TestContext.Current.CancellationToken));
            var duplicate = await hub.EmailManager.SendAsync("app", Message("event"), cancellationToken: TestContext.Current.CancellationToken);

            // validation
            Assert.Equal(EmailError.DeliveryFailed, error.Error);
            Assert.IsType<IOException>(error.InnerException);
            Assert.Equal(EmailSendResult.AlreadyAttempted, duplicate);
            Assert.Equal(1, provider.Calls);
            var logs = string.Join('\n', context.Log.GetRecentEntries().Select(entry => entry.Message));
            Assert.Contains("category=DeliveryFailed", logs);
            Assert.DoesNotContain("secret", logs);
            Assert.DoesNotContain("recipient@example.org", logs);
            Assert.DoesNotContain("subject", logs);
        }

        /// <summary>
        /// Verifies cancellation before submission leaves the logical message available for a later call.
        /// </summary>
        [Fact]
        public async Task PreCancelledSubmissionDoesNotClaim()
        {
            // arrange
            using var hub = CreateHub();
            var provider = new TestProvider();
            hub.EmailManager.RegisterProvider(provider);

            // act
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                hub.EmailManager.SendAsync("app", Message("event"), cancellationToken: new CancellationToken(true)));
            var result = await hub.EmailManager.SendAsync("app", Message("event"), cancellationToken: TestContext.Current.CancellationToken);

            // validation
            Assert.Equal(EmailSendResult.Accepted, result);
            Assert.Equal(1, provider.Calls);
        }

        /// <summary>
        /// Verifies that deadlines have a stable error category and caller cancellation retains its native contract.
        /// </summary>
        /// <param name="cancelCaller">Whether to cancel the caller instead of waiting for the deadline.</param>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task CancellationAndDeadlineRetainUncertainClaims(bool cancelCaller)
        {
            // arrange
            using var hub = CreateHub(new() { ["WebExpress:Email:Profiles:default:TimeoutSeconds"] = "1" });
            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            hub.EmailManager.RegisterProvider(new TestProvider(async (_, _, token) =>
            {
                entered.SetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
            }));
            using var cancellation = new CancellationTokenSource();

            // act
            var pending = hub.EmailManager.SendAsync("app", Message("event"), cancellationToken: cancellation.Token);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            if (cancelCaller) cancellation.Cancel();
            var error = await Record.ExceptionAsync(async () => await pending);
            var duplicate = await hub.EmailManager.SendAsync("app", Message("event"), cancellationToken: TestContext.Current.CancellationToken);

            // validation
            if (cancelCaller) Assert.IsAssignableFrom<OperationCanceledException>(error);
            else Assert.Equal(EmailError.Timeout, Assert.IsType<EmailException>(error).Error);
            Assert.Equal(EmailSendResult.AlreadyAttempted, duplicate);
        }

        /// <summary>
        /// Verifies that graceful shutdown drains admitted mail and refuses subsequent submissions.
        /// </summary>
        [Fact]
        public async Task GracefulShutdownWaitsForAdmittedMail()
        {
            // arrange
            var context = UnitTestFixture.CreateHttpServerContextMock(configuration: Configuration());
            using var hub = UnitTestFixture.CreateComponentHubMock(context);
            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            hub.EmailManager.RegisterProvider(new TestProvider(async (_, _, token) =>
            {
                entered.SetResult();
                await release.Task.WaitAsync(token);
            }));

            // act
            var pending = hub.EmailManager.SendAsync("app", Message(), cancellationToken: TestContext.Current.CancellationToken);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            var stopping = context.Lifetime.StopAsync(TestContext.Current.CancellationToken);
            var rejected = await Assert.ThrowsAsync<EmailException>(() => hub.EmailManager.SendAsync("app", Message(), cancellationToken: TestContext.Current.CancellationToken));
            var waited = !stopping.IsCompleted;
            release.SetResult();
            await stopping.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

            // validation
            Assert.True(waited);
            Assert.Equal(EmailError.Stopping, rejected.Error);
            Assert.Equal(EmailSendResult.Accepted, await pending);
        }

        /// <summary>
        /// Verifies provider registration, unload, replacement and manager disposal boundaries.
        /// </summary>
        [Fact]
        public async Task ProviderLifecycleRejectsCollisionsAndMissingProviders()
        {
            // arrange
            using var hub = CreateHub();
            var first = new TestProvider();
            var second = new TestProvider();
            hub.EmailManager.RegisterProvider(first);

            // act
            Assert.Throws<ArgumentException>(() => hub.EmailManager.RegisterProvider(second));
            Assert.False(hub.EmailManager.UnregisterProvider(second));
            Assert.True(hub.EmailManager.UnregisterProvider(first));
            var missing = await Assert.ThrowsAsync<EmailException>(() => hub.EmailManager.SendAsync("app", Message(), cancellationToken: TestContext.Current.CancellationToken));
            hub.EmailManager.RegisterProvider(second);
            var sent = await hub.EmailManager.SendAsync("app", Message(), cancellationToken: TestContext.Current.CancellationToken);
            hub.EmailManager.Dispose();

            // validation
            Assert.Equal(EmailError.Configuration, missing.Error);
            Assert.Equal(EmailSendResult.Accepted, sent);
            Assert.Throws<ObjectDisposedException>(() => hub.EmailManager.RegisterProvider(first));
            await Assert.ThrowsAsync<ObjectDisposedException>(() => hub.EmailManager.SendAsync("app", Message(), cancellationToken: TestContext.Current.CancellationToken));
        }

        /// <summary>
        /// Verifies that a cluster with only a message bus cannot silently use local deduplication.
        /// </summary>
        [Fact]
        public async Task ClusterWithoutSharedStoreFailsClosed()
        {
            // arrange
            using var hub = CreateHub(new()
            {
                ["WebExpress:Cluster:Peers:0"] = "http://127.0.0.1:1/",
                ["WebExpress:Cluster:Secret"] = Convert.ToBase64String(new byte[32])
            });
            var provider = new TestProvider();
            hub.EmailManager.RegisterProvider(provider);

            // act
            var error = await Assert.ThrowsAsync<EmailException>(() => hub.EmailManager.SendAsync("app", Message(), cancellationToken: TestContext.Current.CancellationToken));

            // validation
            Assert.Equal(EmailError.StoreUnavailable, error.Error);
            Assert.Equal(0, provider.Calls);
        }

        /// <summary>
        /// Verifies that profile errors stop before any external submission is attempted.
        /// </summary>
        /// <param name="setting">The invalid configuration key.</param>
        /// <param name="value">The invalid configuration value.</param>
        [Theory]
        [InlineData("TimeoutSeconds", "0")]
        [InlineData("TimeoutSeconds", "301")]
        [InlineData("Port", "0")]
        [InlineData("Security", "99")]
        [InlineData("Host", "")]
        [InlineData("UserName", "account-without-password")]
        public async Task InvalidProfileFailsBeforeClaim(string setting, string value)
        {
            // arrange
            var settings = SmtpSettings(25);
            settings["WebExpress:Email:Profiles:default:" + setting] = value;
            using var hub = CreateHub(settings);

            // act
            var error = await Assert.ThrowsAsync<EmailException>(() => hub.EmailManager.SendAsync("app", Message(), cancellationToken: TestContext.Current.CancellationToken));

            // validation
            Assert.Equal(EmailError.Configuration, error.Error);
            Assert.Empty(hub.ClusterManager.Store.List("email-attempt"));
        }

        /// <summary>
        /// Verifies that a failed shared store cannot degrade into an unprotected delivery.
        /// </summary>
        [Fact]
        public async Task StoreFailurePreventsProviderCall()
        {
            // arrange
            var directory = Path.Combine(Path.GetTempPath(), "wx-email-" + Guid.NewGuid().ToString("N"));
            using var hub = CreateHub();
            var provider = new TestProvider();
            hub.EmailManager.RegisterProvider(provider);
            hub.ClusterManager.UseStore(new FileClusterStore(directory));
            Directory.Delete(directory, true);
            File.WriteAllText(directory, "blocks directory access");
            try
            {
                // act
                var error = await Assert.ThrowsAsync<EmailException>(() => hub.EmailManager.SendAsync("app", Message(), cancellationToken: TestContext.Current.CancellationToken));

                // validation
                Assert.Equal(EmailError.StoreUnavailable, error.Error);
                Assert.Equal(0, provider.Calls);
            }
            finally
            {
                File.Delete(directory);
            }
        }

        /// <summary>
        /// Verifies profile routing without allowing a profile switch to bypass a logical delivery claim.
        /// </summary>
        [Fact]
        public async Task ProfileSwitchDoesNotBypassDeduplication()
        {
            // arrange
            using var hub = CreateHub(new()
            {
                ["WebExpress:Email:Profiles:other:Provider"] = "test",
                ["WebExpress:Email:Profiles:other:From"] = "other@example.org"
            });
            string sender = null;
            var provider = new TestProvider((message, _, _) =>
            {
                sender = message.From.Mailboxes.Single().Address;
                return Task.CompletedTask;
            });
            hub.EmailManager.RegisterProvider(provider);

            // act
            var accepted = await hub.EmailManager.SendAsync("app", Message("event"), "other", cancellationToken: TestContext.Current.CancellationToken);
            var duplicate = await hub.EmailManager.SendAsync("app", Message("event"), cancellationToken: TestContext.Current.CancellationToken);
            var missing = await Assert.ThrowsAsync<EmailException>(() => hub.EmailManager.SendAsync("app", Message(), "missing", cancellationToken: TestContext.Current.CancellationToken));

            // validation
            Assert.Equal(EmailSendResult.Accepted, accepted);
            Assert.Equal("other@example.org", sender);
            Assert.Equal(EmailSendResult.AlreadyAttempted, duplicate);
            Assert.Equal(EmailError.Configuration, missing.Error);
            Assert.Equal(1, provider.Calls);
        }

        /// <summary>
        /// Verifies that the documented retention boundary permits a fresh attempt after expiry.
        /// </summary>
        [Fact]
        public async Task ClaimExpiresAfterConfiguredRetention()
        {
            // arrange
            using var hub = CreateHub(new() { ["WebExpress:Email:DeduplicationHours"] = "1" });
            var clock = new TestClock();
            hub.ClusterManager.UseStore(new MemoryClusterStore(clock));
            var provider = new TestProvider();
            hub.EmailManager.RegisterProvider(provider);

            // act
            var first = await hub.EmailManager.SendAsync("app", Message("event"), cancellationToken: TestContext.Current.CancellationToken);
            clock.Now += TimeSpan.FromMinutes(59);
            var retained = await hub.EmailManager.SendAsync("app", Message("event"), cancellationToken: TestContext.Current.CancellationToken);
            clock.Now += TimeSpan.FromMinutes(2);
            var expired = await hub.EmailManager.SendAsync("app", Message("event"), cancellationToken: TestContext.Current.CancellationToken);

            // validation
            Assert.Equal(EmailSendResult.Accepted, first);
            Assert.Equal(EmailSendResult.AlreadyAttempted, retained);
            Assert.Equal(EmailSendResult.Accepted, expired);
            Assert.Equal(2, provider.Calls);
        }

        /// <summary>
        /// Verifies that a real SMTP exchange preserves Unicode MIME content and keeps Bcc out of headers.
        /// </summary>
        /// <param name="plainText">Whether to include the plain text alternative.</param>
        /// <param name="html">Whether to include the HTML alternative.</param>
        [Theory]
        [InlineData(true, true)]
        [InlineData(true, false)]
        [InlineData(false, true)]
        public async Task SmtpRoundTripPreservesMimeAndHidesBcc(bool plainText, bool html)
        {
            // arrange
            await using var server = new SmtpServer();
            using var hub = CreateHub(SmtpSettings(server.Port));
            var message = new EmailMessage
            {
                To = ["receiver@example.org"], Bcc = ["hidden@example.org"],
                Subject = "Grüße", TextBody = plainText ? "Plain München" : null,
                HtmlBody = html ? "<p>HTML München</p>" : null,
                Attachments = [new() { FileName = "Prüfung.bin", Content = [0, 128, 255] }]
            };

            // act
            var result = await hub.EmailManager.SendAsync("app", message, cancellationToken: TestContext.Current.CancellationToken);
            var wire = await server.Content.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            using var mime = MimeMessage.Load(new MemoryStream(Encoding.UTF8.GetBytes(wire)), TestContext.Current.CancellationToken);
            using var attachment = new MemoryStream();
            ((MimePart)mime.Attachments.Single()).Content.DecodeTo(attachment, TestContext.Current.CancellationToken);

            // validation
            Assert.Equal(EmailSendResult.Accepted, result);
            Assert.Equal("Grüße", mime.Subject);
            Assert.Equal(plainText ? "Plain München" : null, mime.TextBody?.TrimEnd());
            Assert.Equal(html ? "<p>HTML München</p>" : null, mime.HtmlBody?.TrimEnd());
            Assert.Empty(mime.Bcc);
            Assert.Contains(server.Commands, command => command.Contains("hidden@example.org"));
            Assert.Equal(new byte[] { 0, 128, 255 }, attachment.ToArray());
        }

        /// <summary>
        /// Verifies that mandatory STARTTLS fails without exposing a message to a plaintext-only server.
        /// </summary>
        [Fact]
        public async Task SmtpNeverDowngradesRequiredTls()
        {
            // arrange
            await using var server = new SmtpServer();
            var settings = SmtpSettings(server.Port);
            settings["WebExpress:Email:Profiles:default:Security"] = "StartTls";
            using var hub = CreateHub(settings);

            // act
            var error = await Assert.ThrowsAsync<EmailException>(() => hub.EmailManager.SendAsync("app", Message(), cancellationToken: TestContext.Current.CancellationToken));

            // validation
            Assert.Equal(EmailError.DeliveryFailed, error.Error);
            Assert.DoesNotContain(server.Commands, command => command.StartsWith("MAIL FROM", StringComparison.Ordinal));
        }

        /// <summary>
        /// Verifies that an actual SMTP refusal is normalized and cannot trigger a second attempt.
        /// </summary>
        [Fact]
        public async Task SmtpRecipientRefusalIsNormalized()
        {
            // arrange
            await using var server = new SmtpServer(rejectRecipient: true);
            using var hub = CreateHub(SmtpSettings(server.Port));

            // act
            var error = await Assert.ThrowsAsync<EmailException>(() => hub.EmailManager.SendAsync("app", Message("event"), cancellationToken: TestContext.Current.CancellationToken));
            var duplicate = await hub.EmailManager.SendAsync("app", Message("event"), cancellationToken: TestContext.Current.CancellationToken);

            // validation
            Assert.Equal(EmailError.DeliveryFailed, error.Error);
            Assert.Equal(EmailSendResult.AlreadyAttempted, duplicate);
            Assert.False(server.Content.IsCompletedSuccessfully);
        }

        /// <summary>
        /// Creates a fresh hub so manager state never leaks across test cases.
        /// </summary>
        /// <param name="overrides">The settings replacing the standard test profile.</param>
        /// <returns>A hub owned by the caller.</returns>
        private static ComponentHub CreateHub(Dictionary<string, string> overrides = null)
        {
            return UnitTestFixture.CreateComponentHubMock(UnitTestFixture.CreateHttpServerContextMock(configuration: Configuration(overrides)));
        }

        /// <summary>
        /// Supplies settings through the same binder used by production hosts.
        /// </summary>
        /// <param name="overrides">The optional settings replacing the test defaults.</param>
        /// <returns>The complete configuration snapshot.</returns>
        private static IConfigurationRoot Configuration(Dictionary<string, string> overrides = null)
        {
            var values = new Dictionary<string, string>
            {
                ["WebExpress:Email:Enabled"] = "true",
                ["WebExpress:Email:Profiles:default:Provider"] = "test",
                ["WebExpress:Email:Profiles:default:From"] = "sender@example.org"
            };
            foreach (var entry in overrides ?? []) values[entry.Key] = entry.Value;
            return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        }

        /// <summary>
        /// Configures only the ephemeral loopback server for protocol tests.
        /// </summary>
        /// <param name="port">The port assigned by the operating system.</param>
        /// <returns>The profile overrides for local SMTP.</returns>
        private static Dictionary<string, string> SmtpSettings(int port)
        {
            return new()
            {
                ["WebExpress:Email:Profiles:default:Provider"] = "smtp",
                ["WebExpress:Email:Profiles:default:Host"] = "127.0.0.1",
                ["WebExpress:Email:Profiles:default:Port"] = port.ToString(),
                ["WebExpress:Email:Profiles:default:Security"] = "None"
            };
        }

        /// <summary>
        /// Creates minimal valid content with an optional stable identifier.
        /// </summary>
        /// <param name="id">The logical identifier reused for deduplication tests.</param>
        /// <returns>The application-owned message.</returns>
        private static EmailMessage Message(string id = null)
        {
            return new() { DeliveryId = id ?? Guid.NewGuid().ToString("N"), To = ["receiver@example.org"], Subject = "subject", TextBody = "body" };
        }

        /// <summary>
        /// Captures calls at the provider boundary while allowing deterministic delays and failures.
        /// </summary>
        private sealed class TestProvider : IEmailProvider
        {
            private readonly Func<MimeMessage, EmailProfileSettings, CancellationToken, Task> _send;

            /// <summary>Counts submissions independently of concurrent caller attempts.</summary>
            public int Calls;

            /// <summary>Gets the profile name reserved for this test implementation.</summary>
            public string Name => "test";

            /// <summary>Configures the provider behavior without replacing manager logic.</summary>
            /// <param name="send">The optional controlled submission behavior.</param>
            public TestProvider(Func<MimeMessage, EmailProfileSettings, CancellationToken, Task> send = null)
            {
                _send = send ?? ((_, _, _) => Task.CompletedTask);
            }

            /// <summary>Counts actual submissions independently of duplicate caller attempts.</summary>
            /// <param name="message">The validated MIME snapshot.</param>
            /// <param name="profile">The selected profile copy.</param>
            /// <param name="cancellationToken">The combined deadline and caller cancellation.</param>
            /// <returns>The configured submission task.</returns>
            public Task SendAsync(MimeMessage message, EmailProfileSettings profile, CancellationToken cancellationToken)
            {
                Interlocked.Increment(ref Calls);
                return _send(message, profile, cancellationToken);
            }
        }

        /// <summary>
        /// Advances claim expiration without sleeping for the configured retention period.
        /// </summary>
        private sealed class TestClock : TimeProvider
        {
            /// <summary>Stores the controlled instant used by the cluster store.</summary>
            internal DateTimeOffset Now = DateTimeOffset.UtcNow;

            /// <summary>Supplies deterministic UTC time for claim expiration.</summary>
            /// <returns>The test-controlled instant.</returns>
            public override DateTimeOffset GetUtcNow() => Now;
        }

        /// <summary>
        /// Implements a bounded local SMTP conversation to verify the shipped transport over a real socket.
        /// </summary>
        private sealed class SmtpServer : IAsyncDisposable
        {
            private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
            private readonly CancellationTokenSource _stop = new(TimeSpan.FromSeconds(10));
            private readonly TaskCompletionSource<string> _content = new(TaskCreationOptions.RunContinuationsAsynchronously);
            private readonly Task _worker;

            /// <summary>Gets the ephemeral listener port.</summary>
            public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

            /// <summary>Gets the payload after the SMTP data terminator arrives.</summary>
            public Task<string> Content => _content.Task;

            /// <summary>Gets the observed envelope commands for recipient assertions.</summary>
            public System.Collections.Concurrent.ConcurrentQueue<string> Commands { get; } = new();

            /// <summary>Starts one bounded SMTP session on an isolated loopback port.</summary>
            /// <param name="rejectRecipient">Whether recipient commands return a permanent refusal.</param>
            public SmtpServer(bool rejectRecipient = false)
            {
                _listener.Start();
                _worker = RunAsync(rejectRecipient);
            }

            /// <summary>Serves the minimum SMTP dialogue needed to observe real client serialization.</summary>
            /// <param name="rejectRecipient">Whether recipient commands return a permanent refusal.</param>
            /// <returns>A task ending when the connection closes or the test deadline expires.</returns>
            private async Task RunAsync(bool rejectRecipient)
            {
                try
                {
                    using var client = await _listener.AcceptTcpClientAsync(_stop.Token);
                    await using var stream = client.GetStream();
                    using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
                    await using var writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true)
                        { NewLine = "\r\n", AutoFlush = true };
                    await writer.WriteLineAsync("220 localhost SMTP");
                    while (await reader.ReadLineAsync(_stop.Token) is { } line)
                    {
                        Commands.Enqueue(line);
                        if (line.StartsWith("EHLO", StringComparison.Ordinal))
                        {
                            await writer.WriteLineAsync("250-localhost\r\n250 SIZE 10000000");
                        }
                        else if (line.StartsWith("RCPT", StringComparison.Ordinal) && rejectRecipient)
                        {
                            await writer.WriteLineAsync("550 recipient refused");
                        }
                        else if (line == "DATA")
                        {
                            await writer.WriteLineAsync("354 send data");
                            var content = new StringBuilder();
                            while (await reader.ReadLineAsync(_stop.Token) is { } data && data != ".")
                            {
                                content.Append(data.StartsWith("..", StringComparison.Ordinal) ? data[1..] : data).Append("\r\n");
                            }
                            _content.TrySetResult(content.ToString());
                            await writer.WriteLineAsync("250 accepted");
                        }
                        else if (line == "QUIT")
                        {
                            await writer.WriteLineAsync("221 bye");
                            return;
                        }
                        else await writer.WriteLineAsync("250 ok");
                    }
                }
                catch (OperationCanceledException) when (_stop.IsCancellationRequested)
                {
                    // bounded test shutdown owns this cancellation
                }
                finally
                {
                    _content.TrySetCanceled();
                }
            }

            /// <summary>Stops the listener and joins the session before releasing test resources.</summary>
            /// <returns>A task completing after the session exits.</returns>
            public async ValueTask DisposeAsync()
            {
                await _stop.CancelAsync();
                _listener.Stop();
                await _worker;
                _stop.Dispose();
            }
        }
    }
}
