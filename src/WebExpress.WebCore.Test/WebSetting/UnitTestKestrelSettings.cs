using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using WebExpress.WebCore.WebSetting;

namespace WebExpress.WebCore.Test.WebSetting
{
    /// <summary>
    /// Unit tests for the optional Kestrel settings block and its binding, mirroring how the
    /// server binds its settings from the merged configuration.
    /// </summary>
    public class UnitTestKestrelSettings
    {
        /// <summary>
        /// Binds the given settings document into an HttpServerSettings instance.
        /// </summary>
        /// <param name="json">The settings document.</param>
        /// <returns>The bound settings.</returns>
        private static HttpServerSettings Bind(string json)
        {
            using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));

            return new ConfigurationBuilder()
                .AddJsonStream(stream)
                .Build()
                .GetServerSettings();
        }

        /// <summary>
        /// Tests that a settings document without a kestrel block leaves the property null so the
        /// server keeps its built-in defaults.
        /// </summary>
        [Fact]
        public void MissingBlockIsNull()
        {
            // arrange
            var json = """{ "WebExpress": { "Endpoints": [ { "Uri": "http://localhost/" } ] } }""";

            // act
            var settings = Bind(json);

            // validation
            Assert.Null(settings.Kestrel);
            Assert.Single(settings.Endpoints);
            Assert.Equal("http://localhost/", settings.Endpoints[0].Uri);
        }

        /// <summary>
        /// Tests that the public URI is bound separately from an internal listener binding.
        /// </summary>
        [Fact]
        public void ExternalUriIsBoundIndependentlyOfEndpoint()
        {
            // arrange
            var json = """{ "WebExpress": { "Endpoints": [ { "Uri": "http://0.0.0.0:8080/" } ], "ExternalUri": "https://www.example.com/" } }""";

            // act
            var settings = Bind(json);

            // validation
            Assert.Equal("http://0.0.0.0:8080/", settings.Endpoints[0].Uri);
            Assert.Equal("https://www.example.com/", settings.ExternalUri);
        }

        /// <summary>
        /// Tests that all kestrel settings are read when present.
        /// </summary>
        [Fact]
        public void FullBlockIsBound()
        {
            // arrange
            var json = """
                {
                  "WebExpress": {
                    "Endpoints": [ { "Uri": "http://localhost/" } ],
                    "Kestrel": {
                      "MaxConcurrentConnections": 300,
                      "MaxRequestBodySize": 3000000000,
                      "MaxRequestHeadersTotalSize": 65536,
                      "AllowSynchronousIO": false,
                      "AllowResponseHeaderCompression": false,
                      "AddServerHeader": false,
                      "MaxConcurrentUpgradedConnections": 1000,
                      "MaxRequestBufferSize": 2097152,
                      "MaxResponseBufferSize": 131072,
                      "MaxRequestLineSize": 16384,
                      "KeepAliveTimeout": 90,
                      "RequestHeadersTimeout": 15
                    }
                  }
                }
                """;

            // act
            var kestrel = Bind(json).Kestrel;

            // validation
            Assert.NotNull(kestrel);
            Assert.Equal(300, kestrel.MaxConcurrentConnections);
            Assert.Equal(3000000000, kestrel.MaxRequestBodySize);
            Assert.Equal(65536, kestrel.MaxRequestHeadersTotalSize);
            Assert.False(kestrel.AllowSynchronousIO);
            Assert.False(kestrel.AllowResponseHeaderCompression);
            Assert.False(kestrel.AddServerHeader);
            Assert.Equal(1000, kestrel.MaxConcurrentUpgradedConnections);
            Assert.Equal(2097152, kestrel.MaxRequestBufferSize);
            Assert.Equal(131072, kestrel.MaxResponseBufferSize);
            Assert.Equal(16384, kestrel.MaxRequestLineSize);
            Assert.Equal(90, kestrel.KeepAliveTimeout);
            Assert.Equal(15, kestrel.RequestHeadersTimeout);
        }

        /// <summary>
        /// Tests that individual settings are independently optional: keys that are not present
        /// remain null, so only explicitly configured values override the defaults.
        /// </summary>
        [Fact]
        public void OmittedKeysRemainNull()
        {
            // arrange
            var json = """{ "WebExpress": { "Kestrel": { "AddServerHeader": false } } }""";

            // act
            var kestrel = Bind(json).Kestrel;

            // validation
            Assert.NotNull(kestrel);
            Assert.False(kestrel.AddServerHeader);
            Assert.Null(kestrel.AllowSynchronousIO);
            Assert.Null(kestrel.AllowResponseHeaderCompression);
            Assert.Null(kestrel.MaxConcurrentConnections);
            Assert.Null(kestrel.MaxRequestBodySize);
            Assert.Null(kestrel.MaxRequestHeadersTotalSize);
            Assert.Null(kestrel.MaxConcurrentUpgradedConnections);
            Assert.Null(kestrel.MaxRequestBufferSize);
            Assert.Null(kestrel.MaxResponseBufferSize);
            Assert.Null(kestrel.MaxRequestLineSize);
            Assert.Null(kestrel.KeepAliveTimeout);
            Assert.Null(kestrel.RequestHeadersTimeout);
        }

        /// <summary>
        /// Tests that keys are matched regardless of their casing, so a hand-written file does
        /// not have to follow the property names to the letter.
        /// </summary>
        [Fact]
        public void KeysAreCaseInsensitive()
        {
            // arrange
            var json = """{ "webexpress": { "kestrel": { "maxconcurrentconnections": 300 } } }""";

            // act
            var kestrel = Bind(json).Kestrel;

            // validation
            Assert.Equal(300, kestrel?.MaxConcurrentConnections);
        }

        /// <summary>
        /// Tests that the protocols key is read and resolved to the matching Kestrel value,
        /// including case-insensitive parsing.
        /// </summary>
        [Theory]
        [InlineData("Http1", HttpProtocols.Http1)]
        [InlineData("Http2", HttpProtocols.Http2)]
        [InlineData("Http1AndHttp2", HttpProtocols.Http1AndHttp2)]
        [InlineData("http2", HttpProtocols.Http2)]
        public void ProtocolsAreResolved(string value, HttpProtocols expected)
        {
            // arrange
            var json = $$"""{ "WebExpress": { "Kestrel": { "Protocols": "{{value}}" } } }""";

            // act
            var kestrel = Bind(json).Kestrel;

            // validation
            Assert.Equal(value, kestrel.Protocols);
            Assert.Equal(expected, kestrel.ResolveProtocols());
        }

        /// <summary>
        /// Tests that a missing protocols key resolves to null so the Kestrel default is kept.
        /// </summary>
        [Fact]
        public void ProtocolsDefaultToNull()
        {
            // arrange
            var json = """{ "WebExpress": { "Kestrel": { "AddServerHeader": true } } }""";

            // act
            var kestrel = Bind(json).Kestrel;

            // validation
            Assert.Null(kestrel.Protocols);
            Assert.Null(kestrel.ResolveProtocols());
        }

        /// <summary>
        /// Tests that an unrecognised protocols value resolves to null instead of applying an
        /// unintended restriction, so a typo cannot silently disable HTTP/2.
        /// </summary>
        [Theory]
        [InlineData("Http9")]
        [InlineData("999")]
        [InlineData("nonsense")]
        public void UnknownProtocolsResolveToNull(string value)
        {
            // arrange
            var json = $$"""{ "WebExpress": { "Kestrel": { "Protocols": "{{value}}" } } }""";

            // act
            var kestrel = Bind(json).Kestrel;

            // validation
            Assert.Null(kestrel.ResolveProtocols());
        }

        /// <summary>
        /// Tests that a document without a server section still yields usable settings, every
        /// value at its default, rather than nothing to start from.
        /// </summary>
        [Fact]
        public void MissingServerSectionYieldsDefaults()
        {
            // arrange
            var json = """{ "Plugins": { "some.plugin": { "Key": "value" } } }""";

            // act
            var settings = Bind(json);

            // validation
            Assert.NotNull(settings);
            Assert.Empty(settings.Endpoints);
            Assert.Null(settings.Kestrel);
            Assert.Null(settings.Session);
            Assert.Equal("Off", settings.Log.Mode);
        }
    }
}
