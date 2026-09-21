using Microsoft.Extensions.Configuration;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using WebExpress.WebCore.Test.Fixture;
using WebExpress.WebCore.WebCertificate;
using WebExpress.WebCore.WebLog;
using WebExpress.WebCore.WebSetting;

namespace WebExpress.WebCore.Test.Manager
{
    /// <summary>
    /// Exercises real PFX loading, provider substitution, validity boundaries and hosting integration.
    /// </summary>
    [Collection("NonParallelTests")]
    public sealed class UnitTestCertificateManager : IDisposable
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "webexpress-certificates-" + Guid.NewGuid().ToString("N"));
        private readonly TestClock _clock = new();

        /// <summary>
        /// Isolates generated certificates from the repository and the operating system certificate store.
        /// </summary>
        public UnitTestCertificateManager()
        {
            Directory.CreateDirectory(_directory);
        }

        /// <summary>
        /// Provides deterministic UTC validity boundaries without sleeping or changing the machine clock.
        /// </summary>
        private sealed class TestClock : TimeProvider
        {
            /// <summary>
            /// Gets or sets the instant observed by certificate validation.
            /// </summary>
            public DateTimeOffset Now { get; set; } = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds());

            /// <summary>
            /// Supplies the test instant to the manager.
            /// </summary>
            /// <returns>The configured UTC instant.</returns>
            public override DateTimeOffset GetUtcNow() => Now;
        }

        /// <summary>
        /// Models a module that supplies certificates without any file access.
        /// </summary>
        /// <param name="pfx">The private material owned by the test provider.</param>
        private sealed class MemoryStore(byte[] pfx) : ICertificateStore
        {
            /// <summary>
            /// Gets the provider name used to select the in-memory test material.
            /// </summary>
            public string Name => "memory";

            /// <summary>
            /// Gets the number of loads so resolution can prove it does not revisit the provider.
            /// </summary>
            public int LoadCount { get; private set; }

            /// <summary>
            /// Transfers fresh material so cached resolution can be distinguished from repeated provider access.
            /// </summary>
            /// <param name="reference">The test reference accepted without filesystem access.</param>
            /// <param name="password">The password protecting the in-memory PFX.</param>
            /// <returns>Newly owned certificate material for the manager.</returns>
            public CertificateMaterial Load(string reference, string password)
            {
                LoadCount++;
                return new CertificateMaterial(X509CertificateLoader.LoadPkcs12(pfx, password, X509KeyStorageFlags.EphemeralKeySet));
            }
        }

        /// <summary>
        /// Models a provider error that would expose secrets if its exception were logged verbatim.
        /// </summary>
        private sealed class FailingStore : ICertificateStore
        {
            /// <summary>
            /// Gets the provider name used to select the failure scenario.
            /// </summary>
            public string Name => "failure";

            /// <summary>
            /// Simulates an unsafe provider exception to verify that diagnostics never expose credentials.
            /// </summary>
            /// <param name="reference">The unused test reference.</param>
            /// <param name="password">The secret deliberately included in the simulated exception.</param>
            /// <returns>No material because the simulated provider always fails.</returns>
            public CertificateMaterial Load(string reference, string password) => throw new IOException(password);
        }

        /// <summary>
        /// Creates a short-lived test leaf with controlled identity, key usage and validity.
        /// </summary>
        /// <param name="host">The subject alternative DNS name.</param>
        /// <param name="notBefore">The beginning of the validity interval.</param>
        /// <param name="notAfter">The end of the validity interval.</param>
        /// <param name="serverUsage">Whether the EKU permits server authentication.</param>
        /// <param name="certificateAuthority">Whether the certificate is a CA certificate.</param>
        /// <param name="signingUsage">Whether the key usage permits digital signatures.</param>
        /// <returns>A certificate with its private key owned by the caller.</returns>
        private static X509Certificate2 CreateCertificate(string host, DateTimeOffset notBefore, DateTimeOffset notAfter,
            bool serverUsage = true, bool certificateAuthority = false, bool signingUsage = true)
        {
            using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var request = new CertificateRequest("CN=" + host, key, HashAlgorithmName.SHA256);
            var names = new SubjectAlternativeNameBuilder();
            names.AddDnsName(host);
            names.AddIpAddress(IPAddress.Loopback);
            request.CertificateExtensions.Add(names.Build());
            request.CertificateExtensions.Add(new X509BasicConstraintsExtension(certificateAuthority, false, 0, true));
            request.CertificateExtensions.Add(new X509KeyUsageExtension(
                signingUsage ? X509KeyUsageFlags.DigitalSignature : X509KeyUsageFlags.KeyEncipherment, true));
            request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
                new OidCollection { new Oid(serverUsage ? "1.3.6.1.5.5.7.3.1" : "1.3.6.1.5.5.7.3.2") }, false));
            return request.CreateSelfSigned(notBefore, notAfter);
        }

        /// <summary>
        /// Writes real password-protected material so tests exercise the shipped file store.
        /// </summary>
        /// <param name="fileName">The file name within the isolated test directory.</param>
        /// <param name="host">The subject alternative DNS name.</param>
        /// <param name="days">The remaining validity in days.</param>
        /// <param name="password">The PFX password used by the loader.</param>
        /// <returns>The absolute file path.</returns>
        private string WriteCertificate(string fileName = "server.pfx", string host = "localhost", int days = 90, string password = "test-secret")
        {
            using var certificate = CreateCertificate(host, _clock.Now.AddDays(-2), _clock.Now.AddDays(days));
            var path = Path.Combine(_directory, fileName);
            File.WriteAllBytes(path, certificate.Export(X509ContentType.Pfx, password));
            return path;
        }

        /// <summary>
        /// Builds the smallest inventory that refers to the generated PFX file.
        /// </summary>
        /// <returns>The independent settings object for one test.</returns>
        private HttpServerSettings Settings() => new()
        {
            Certificates = new CertificateManagerSettings
            {
                Directory = _directory,
                Items = [new CertificateSettings { Alias = "primary", Reference = "server.pfx", Password = "test-secret", HostNames = ["localhost"] }]
            }
        };

        /// <summary>
        /// Proves aliases and normalized hostnames share cached material with inspectable metadata.
        /// </summary>
        [Fact]
        public void FileInventoryResolvesAliasesHostsAndMetadata()
        {
            // arrange
            var path = WriteCertificate();
            using var manager = new CertificateManager(timeProvider: _clock);

            // act
            manager.Load(Settings());
            File.Delete(path);
            var material = manager.Resolve("PRIMARY");

            // validation
            Assert.Same(material, manager.Resolve("LOCALHOST."));
            var info = Assert.Single(manager.GetCertificates());
            Assert.True(info.IsUsable);
            Assert.Equal(CertificateStatus.Valid, info.Status);
            Assert.Equal(material.Certificate.Issuer, info.Issuer);
            Assert.Equal(_clock.Now.AddDays(90), info.NotAfter);
            Assert.Equal(material.Certificate.Thumbprint, info.Thumbprint);
        }

        /// <summary>
        /// Keeps two configured names independent while preserving legacy absolute PFX references.
        /// </summary>
        [Fact]
        public void MultipleInlineEndpointsResolveDifferentCertificates()
        {
            // arrange
            var first = WriteCertificate("first.pfx", "first.example");
            var second = WriteCertificate("second.pfx", "second.example");
            var settings = new HttpServerSettings
            {
                Endpoints =
                [
                    new() { Uri = "https://first.example:443", PfxFile = first, Password = "test-secret", CertificateAlias = "first" },
                    new() { Uri = "HTTPS://second.example:8443", PfxFile = second, Password = "test-secret" }
                ]
            };
            using var manager = new CertificateManager(timeProvider: _clock);

            // act
            manager.Load(settings);

            // validation
            Assert.Same(manager.Resolve("first"), manager.Resolve("FIRST.EXAMPLE"));
            Assert.Same(manager.Resolve(settings.Endpoints[1]), manager.Resolve("second.example"));
            Assert.NotEqual(manager.Resolve("first").Certificate.Thumbprint, manager.Resolve("second.example").Certificate.Thumbprint);
        }

        /// <summary>
        /// Separates TLS suitability failures from provider loading failures.
        /// </summary>
        /// <param name="failure">The suitability rule the generated material violates.</param>
        /// <param name="expected">The status flag callers must be able to inspect.</param>
        [Theory]
        [InlineData("expired", CertificateStatus.Expired)]
        [InlineData("future", CertificateStatus.NotYetValid)]
        [InlineData("keyless", CertificateStatus.MissingPrivateKey)]
        [InlineData("client", CertificateStatus.InvalidUsage)]
        [InlineData("ca", CertificateStatus.InvalidUsage)]
        [InlineData("usage", CertificateStatus.InvalidUsage)]
        [InlineData("hostname", CertificateStatus.HostNameMismatch)]
        public void InvalidCertificatesRemainInspectableButCannotResolve(string failure, CertificateStatus expected)
        {
            // arrange
            using var certificate = CreateCertificate(failure == "hostname" ? "other.example" : "localhost",
                _clock.Now.AddDays(failure == "future" ? 1 : -3), _clock.Now.AddDays(failure == "expired" ? -1 : 90),
                serverUsage: failure != "client", certificateAuthority: failure == "ca", signingUsage: failure != "usage");
            using var publicOnly = X509CertificateLoader.LoadCertificate(certificate.Export(X509ContentType.Cert));
            File.WriteAllBytes(Path.Combine(_directory, "server.pfx"),
                (failure == "keyless" ? publicOnly : certificate).Export(X509ContentType.Pfx, "test-secret"));
            using var manager = new CertificateManager(timeProvider: _clock);

            // act
            manager.Load(Settings());

            // validation
            var info = Assert.Single(manager.GetCertificates());
            Assert.True(info.Status.HasFlag(expected));
            Assert.False(info.IsUsable);
            Assert.Throws<InvalidOperationException>(() => manager.Resolve("primary"));
        }

        /// <summary>
        /// Reports unreadable PFX files without returning unusable material or leaking credentials.
        /// </summary>
        /// <param name="failure">The file or credential boundary to violate.</param>
        [Theory]
        [InlineData("missing")]
        [InlineData("password")]
        [InlineData("corrupt")]
        public void LoadFailuresAreSafeAndInspectable(string failure)
        {
            // arrange
            if (failure != "missing") { WriteCertificate(); }
            if (failure == "corrupt") { File.WriteAllText(Path.Combine(_directory, "server.pfx"), "invalid pfx"); }
            var settings = Settings();
            settings.Certificates.Items[0].Password = "never-log-this-password";
            var log = new Log { LogMode = LogMode.Off };
            using var manager = new CertificateManager(log, _clock);

            // act
            manager.Load(settings);

            // validation
            Assert.Equal(CertificateStatus.LoadFailed, Assert.Single(manager.GetCertificates()).Status);
            Assert.Throws<InvalidOperationException>(() => manager.Resolve("primary"));
            Assert.DoesNotContain(log.GetRecentEntries(), x => x.Message.Contains("never-log-this-password"));
            Assert.True(log.ErrorCount > 0);
        }

        /// <summary>
        /// Makes expiry warnings configurable and validates the exact validity endpoint on later resolutions.
        /// </summary>
        [Fact]
        public void ThresholdsAndClockChangesControlStatus()
        {
            // arrange
            WriteCertificate(days: 10);
            var settings = Settings();
            settings.Certificates.WarningThresholdDays = [14, 7];
            var log = new Log { LogMode = LogMode.Off };
            using var manager = new CertificateManager(log, _clock);

            // act
            manager.Load(settings);

            // validation
            Assert.Equal(CertificateStatus.ExpiringSoon, Assert.Single(manager.GetCertificates()).Status);
            Assert.Equal(1, log.WarningCount);
            Assert.Contains(log.GetRecentEntries(), x => x.Message.Contains("primary") && x.Message.Contains("14"));
            Assert.NotNull(manager.Resolve("primary"));

            // act
            _clock.Now = _clock.Now.AddDays(10);

            // validation
            Assert.Equal(CertificateStatus.Expired, Assert.Single(manager.GetCertificates()).Status);
            Assert.Throws<InvalidOperationException>(() => manager.Resolve("primary"));
        }

        /// <summary>
        /// Allows deployments to suppress expiry warnings without disabling certificate validity checks.
        /// </summary>
        [Fact]
        public void EmptyThresholdsDisableWarnings()
        {
            // arrange
            WriteCertificate(days: 1);
            var settings = Settings();
            settings.Certificates.WarningThresholdDays = [];
            var log = new Log { LogMode = LogMode.Off };
            using var manager = new CertificateManager(log, _clock);

            // act
            manager.Load(settings);

            // validation
            Assert.Equal(CertificateStatus.Valid, Assert.Single(manager.GetCertificates()).Status);
            Assert.Equal(0, log.WarningCount);
        }

        /// <summary>
        /// Prevents endpoint aliases from bypassing hostname validation or choosing an arbitrary fallback.
        /// </summary>
        [Fact]
        public void EndpointResolutionValidatesHostAndMissingMappings()
        {
            // arrange
            WriteCertificate();
            using var manager = new CertificateManager(timeProvider: _clock);
            manager.Load(Settings());

            // act
            var concrete = manager.Resolve(new EndpointSettings { Uri = "https://localhost:443" });
            var wildcard = manager.Resolve(new EndpointSettings { Uri = "https://*:443", CertificateAlias = "primary" });

            // validation
            Assert.NotNull(concrete);
            Assert.Same(concrete, wildcard);
            Assert.Throws<InvalidOperationException>(() => manager.Resolve(new EndpointSettings { Uri = "https://other.example", CertificateAlias = "primary" }));
            Assert.Throws<KeyNotFoundException>(() => manager.Resolve("unknown"));
            Assert.Throws<KeyNotFoundException>(() => manager.Resolve(new EndpointSettings { Uri = "https://*:443" }));
        }

        /// <summary>
        /// Preserves an unambiguous inventory even when configuration is supplied by several files.
        /// </summary>
        /// <param name="failure">The invalid configuration to construct.</param>
        [Theory]
        [InlineData("alias")]
        [InlineData("host")]
        [InlineData("alias-host")]
        [InlineData("store")]
        [InlineData("threshold")]
        [InlineData("wildcard")]
        public void ConflictingOrInvalidConfigurationIsRejected(string failure)
        {
            // arrange
            WriteCertificate();
            var settings = Settings();
            switch (failure)
            {
                case "alias": settings.Certificates.Items.Add(new() { Alias = "PRIMARY", Reference = "server.pfx" }); break;
                case "host": settings.Certificates.Items.Add(new() { Alias = "other", Reference = "server.pfx", HostNames = ["LOCALHOST."] }); break;
                case "alias-host": settings.Certificates.Items.Add(new() { Alias = "localhost", Reference = "server.pfx" }); break;
                case "store": settings.Certificates.Items[0].Store = "unknown"; break;
                case "threshold": settings.Certificates.WarningThresholdDays = [-1]; break;
                case "wildcard": settings.Certificates.Items[0].HostNames = ["*.example.com"]; break;
            }
            using var manager = new CertificateManager(timeProvider: _clock);

            // act
            var exception = Record.Exception(() => manager.Load(settings));

            // validation
            Assert.NotNull(exception);
            Assert.Empty(manager.GetCertificates());
        }

        /// <summary>
        /// Demonstrates that providers can change without introducing file access in applications.
        /// </summary>
        [Fact]
        public void ExternalStoreSupportsCachedResolutionAndSafeReload()
        {
            // arrange
            using var certificate = CreateCertificate("localhost", _clock.Now.AddDays(-1), _clock.Now.AddDays(90));
            var store = new MemoryStore(certificate.Export(X509ContentType.Pfx, "test-secret"));
            using var manager = new CertificateManager(timeProvider: _clock);
            manager.RegisterStore(store);
            var settings = Settings();
            settings.Certificates.Items[0].Store = "memory";
            settings.Certificates.Items[0].Reference = "provider-reference";

            // act
            manager.Load(settings);
            var borrowed = manager.Resolve("primary");

            // validation
            Assert.Same(borrowed, manager.Resolve("localhost"));
            Assert.Equal(1, store.LoadCount);

            // act
            manager.Load(settings);

            // validation
            Assert.NotSame(borrowed, manager.Resolve("primary"));
            Assert.True(borrowed.Certificate.HasPrivateKey);

            // act
            manager.Dispose();

            // validation
            Assert.Throws<ObjectDisposedException>(() => manager.Resolve("primary"));
            Assert.Equal(IntPtr.Zero, borrowed.Certificate.Handle);
        }

        /// <summary>
        /// Keeps provider exceptions containing credentials out of the shared log.
        /// </summary>
        [Fact]
        public void ProviderExceptionDoesNotLeakPassword()
        {
            // arrange
            var log = new Log { LogMode = LogMode.Off };
            using var manager = new CertificateManager(log, _clock);
            manager.RegisterStore(new FailingStore());
            var settings = Settings();
            settings.Certificates.Items[0].Store = "failure";

            // act
            manager.Load(settings);

            // validation
            Assert.DoesNotContain(log.GetRecentEntries(), x => x.Message.Contains("test-secret"));
            Assert.Equal(CertificateStatus.LoadFailed, Assert.Single(manager.GetCertificates()).Status);
        }

        /// <summary>
        /// Verifies that existing configuration binding exposes the complete certificate configuration.
        /// </summary>
        [Fact]
        public void SettingsBindCertificatesAndEndpointAliases()
        {
            // arrange
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes("""
                { "WebExpress": {
                    "Certificates": { "Directory": "./ssl", "WarningThresholdDays": [21, 3],
                        "Items": [{ "Alias": "site", "Reference": "site.pfx", "Password": "configured", "HostNames": ["site.example"] }] },
                    "Endpoints": [{ "Uri": "https://*:443", "CertificateAlias": "site" }]
                } }
                """));
            var configuration = new ConfigurationBuilder().AddJsonStream(stream).Build();

            // act
            var settings = configuration.GetServerSettings();

            // validation
            Assert.Equal("./ssl", settings.Certificates.Directory);
            Assert.Equal([21, 3], settings.Certificates.WarningThresholdDays);
            Assert.Equal("configured", settings.Certificates.Items[0].Password);
            Assert.Equal("file", settings.Certificates.Items[0].Store);
            Assert.Equal("site", settings.Endpoints[0].CertificateAlias);
        }

        /// <summary>
        /// Preserves configured empty arrays so operators can disable expiry warnings through JSON.
        /// </summary>
        [Fact]
        public void EmptyWarningArraySurvivesConfigurationBinding()
        {
            // arrange
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(
                """{ "WebExpress": { "Certificates": { "WarningThresholdDays": [] } } }"""));

            // act
            var settings = new ConfigurationBuilder().AddJsonStream(stream).Build().GetServerSettings();

            // validation
            Assert.NotNull(settings.Certificates.WarningThresholdDays);
            Assert.Empty(settings.Certificates.WarningThresholdDays);
        }

        /// <summary>
        /// Verifies failed replacement configuration leaves the currently served inventory intact.
        /// </summary>
        [Fact]
        public void FailedReloadKeepsExistingInventory()
        {
            // arrange
            WriteCertificate();
            using var manager = new CertificateManager(timeProvider: _clock);
            var settings = Settings();
            manager.Load(settings);
            var current = manager.Resolve("primary");
            settings.Certificates.Items.Add(new() { Alias = "other", Store = "missing", Reference = "anything" });

            // act
            var exception = Record.Exception(() => manager.Load(settings));

            // validation
            Assert.IsType<InvalidOperationException>(exception);
            Assert.Same(current, manager.Resolve("primary"));
            Assert.True(current.Certificate.HasPrivateKey);
        }

        /// <summary>
        /// Keeps one alias usable for several endpoints without reloading its PFX for each listener.
        /// </summary>
        [Fact]
        public void InlineAliasCanBeSharedAcrossPorts()
        {
            // arrange
            var path = WriteCertificate();
            var settings = new HttpServerSettings
            {
                Endpoints =
                [
                    new() { Uri = "https://localhost:443", CertificateAlias = "site" },
                    new() { Uri = "https://127.0.0.1:8443", CertificateAlias = "site", PfxFile = path, Password = "test-secret" }
                ]
            };
            using var manager = new CertificateManager(timeProvider: _clock);

            // act
            manager.Load(settings);

            // validation
            Assert.Single(manager.GetCertificates());
            Assert.Same(manager.Resolve(settings.Endpoints[0]), manager.Resolve(settings.Endpoints[1]));
            Assert.Same(manager.Resolve("localhost"), manager.Resolve("127.0.0.1"));
        }

        /// <summary>
        /// Preserves supplied issuer certificates and presents the intermediate during the TLS handshake.
        /// </summary>
        /// <returns>A task that completes after the client has observed the supplied intermediate.</returns>
        [Fact]
        public async Task HttpsListenerPresentsSuppliedIntermediateChain()
        {
            // arrange
            using var rootKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var rootRequest = new CertificateRequest("CN=WebExpress Test Root", rootKey, HashAlgorithmName.SHA256);
            rootRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
            rootRequest.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign, true));
            using var root = rootRequest.CreateSelfSigned(_clock.Now.AddDays(-2), _clock.Now.AddDays(100));

            using var issuerKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var issuerRequest = new CertificateRequest("CN=WebExpress Test Intermediate", issuerKey, HashAlgorithmName.SHA256);
            issuerRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
            issuerRequest.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign, true));
            using var issuerPublic = issuerRequest.Create(root, _clock.Now.AddDays(-1), _clock.Now.AddDays(95), RandomNumberGenerator.GetBytes(16));
            using var issuer = issuerPublic.CopyWithPrivateKey(issuerKey);

            using var leafKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var request = new CertificateRequest("CN=localhost", leafKey, HashAlgorithmName.SHA256);
            var names = new SubjectAlternativeNameBuilder();
            names.AddDnsName("localhost");
            request.CertificateExtensions.Add(names.Build());
            request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
            request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true));
            request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection { new("1.3.6.1.5.5.7.3.1") }, false));
            using var leafPublic = request.Create(issuer, _clock.Now.AddHours(-1), _clock.Now.AddDays(90), RandomNumberGenerator.GetBytes(16));
            using var leaf = leafPublic.CopyWithPrivateKey(leafKey);
            using var rootPublic = X509CertificateLoader.LoadCertificate(root.Export(X509ContentType.Cert));
            var bundle = new X509Certificate2Collection { leaf, issuerPublic, rootPublic };
            File.WriteAllBytes(Path.Combine(_directory, "server.pfx"), bundle.Export(X509ContentType.Pfx, "test-secret"));

            var settings = Settings();
            var port = GetAvailablePort();
            settings.Endpoints = [new() { Uri = $"https://*:{port}", CertificateAlias = "primary" }];
            var server = new HttpServer(UnitTestFixture.CreateHttpServerContextMock()) { Settings = settings };
            try
            {
                server.Start();
                var material = server.HttpServerContext.CertificateManager.Resolve("primary");
                using var client = new TcpClient();
                await client.ConnectAsync(IPAddress.Loopback, port, TestContext.Current.CancellationToken);
                var receivedIntermediate = false;
                using var tls = new SslStream(client.GetStream(), false, (_, _, chain, _) =>
                {
                    receivedIntermediate = chain.ChainElements.Cast<X509ChainElement>()
                        .Any(x => x.Certificate.Thumbprint == issuer.Thumbprint);
                    return true;
                });
                // act
                await tls.AuthenticateAsClientAsync(new SslClientAuthenticationOptions { TargetHost = "localhost" },
                    TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

                // validation
                Assert.Equal(2, material.Chain.Count);
                Assert.Contains(material.Chain, x => x.Thumbprint == issuer.Thumbprint);
                Assert.True(receivedIntermediate);
            }
            finally { server.Stop(); }
        }

        /// <summary>
        /// Proves the real Kestrel listener receives manager-owned private material and completes TLS.
        /// </summary>
        /// <returns>A task that completes after the TLS peer certificate has been inspected.</returns>
        [Fact]
        public async Task HttpsListenerCompletesHandshakeWithManagedCertificate()
        {
            // arrange
            WriteCertificate();
            var settings = Settings();
            var port = GetAvailablePort();
            settings.Endpoints = [new() { Uri = $"https://127.0.0.1:{port}", CertificateAlias = "primary" }];
            var server = new HttpServer(UnitTestFixture.CreateHttpServerContextMock()) { Settings = settings };
            try
            {
                server.Start();
                using var client = new TcpClient();
                await client.ConnectAsync(IPAddress.Loopback, port, TestContext.Current.CancellationToken).AsTask().WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
                using var tls = new SslStream(client.GetStream(), false, (_, _, _, _) => true);

                // act
                await tls.AuthenticateAsClientAsync(new SslClientAuthenticationOptions { TargetHost = "localhost" }, TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

                // validation
                Assert.True(tls.IsAuthenticated);
                Assert.Equal(server.HttpServerContext.CertificateManager.Resolve("primary").Certificate.Thumbprint,
                    tls.RemoteCertificate.GetCertHashString());
            }
            finally { server.Stop(); }
        }

        /// <summary>
        /// Ensures development HTTP never requires a certificate directory or a configured certificate.
        /// </summary>
        /// <returns>A task that completes after connecting to the HTTP listener.</returns>
        [Fact]
        public async Task HttpDevelopmentStartsWithoutCertificates()
        {
            // arrange
            var port = GetAvailablePort();
            var server = new HttpServer(UnitTestFixture.CreateHttpServerContextMock())
            {
                Settings = new HttpServerSettings { Endpoints = [new() { Uri = $"http://127.0.0.1:{port}" }] }
            };

            // act
            try
            {
                server.Start();
                using var client = new TcpClient();
                await client.ConnectAsync(IPAddress.Loopback, port, TestContext.Current.CancellationToken).AsTask().WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

                // validation
                Assert.Empty(server.HttpServerContext.CertificateManager.GetCertificates());
            }
            finally { server.Stop(); }
        }

        /// <summary>
        /// Prevents a failed HTTPS configuration from leaving any HTTP listener running.
        /// </summary>
        /// <returns>A task that completes after confirming the port remains closed.</returns>
        [Fact]
        public async Task InvalidHttpsAbortsStartupBeforeHttpBinds()
        {
            // arrange
            var port = GetAvailablePort();
            var server = new HttpServer(UnitTestFixture.CreateHttpServerContextMock())
            {
                Settings = new HttpServerSettings
                {
                    Endpoints = [new() { Uri = $"http://127.0.0.1:{port}" }, new() { Uri = "https://127.0.0.1:443", CertificateAlias = "missing" }]
                }
            };
            // act
            var exception = Record.Exception(() => server.Start());

            // validation
            Assert.IsType<KeyNotFoundException>(exception);
            using var client = new TcpClient();
            await Assert.ThrowsAsync<SocketException>(async () => await client.ConnectAsync(IPAddress.Loopback, port, TestContext.Current.CancellationToken));
            server.Stop();
        }

        /// <summary>
        /// Finds an ephemeral loopback port without relying on a machine-specific fixed test port.
        /// </summary>
        /// <returns>The port just released for the test listener.</returns>
        private static int GetAvailablePort()
        {
            using var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }

        /// <summary>
        /// Removes only the isolated directory created by this test instance.
        /// </summary>
        public void Dispose()
        {
            Directory.Delete(_directory, true);
        }
    }
}
