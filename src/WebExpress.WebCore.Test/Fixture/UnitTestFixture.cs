using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Configuration;
using System.Globalization;
using System.Net;
using System.Reflection;
using System.Text;
using WebExpress.WebCore.WebApplication;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebCore.WebEndpoint;
using WebExpress.WebCore.WebLog;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebPage;
using WebExpress.WebCore.WebPlugin;
using WebExpress.WebCore.WebUri;

namespace WebExpress.WebCore.Test.Fixture
{
    /// <summary>
    /// A fixture class for unit tests, providing various mock objects and utility methods.
    /// </summary>
    public class UnitTestFixture : IDisposable
    {
        private static readonly string[] _separator = ["\r\n", "\r", "\n"];

        /// <summary>
        /// Initializes a new instance of the class and boot the component manager.
        /// </summary>
        public UnitTestFixture()
        {
        }

        /// <summary>
        /// Create a fake server context.
        /// </summary>
        /// <param name="settingsPath">
        /// The settings directory. Defaults to a directory of its own, so a test that deploys a
        /// settings file never sees one left behind by another.
        /// </param>
        /// <param name="configuration">The configuration. Defaults to an empty one.</param>
        /// <param name="externalUri">The optional public base URI.</param>
        /// <returns>The server context.</returns>
        public static IHttpServerContext CreateHttpServerContextMock(string settingsPath = null, IConfigurationRoot configuration = null, string externalUri = null)
        {
            return new HttpServerContext
            (
                new RouteEndpoint("server"),
                [],
                Path.Combine(Environment.CurrentDirectory, Guid.NewGuid().ToString()),
                Environment.CurrentDirectory,
                Environment.CurrentDirectory,
                settingsPath ?? Path.Combine(Environment.CurrentDirectory, Guid.NewGuid().ToString()),
                configuration ?? new ConfigurationBuilder().Build(),
                CultureInfo.GetCultureInfo("en"),
                new Log() { LogMode = LogMode.Off },
                null,
                externalUri: externalUri
            );
        }

        /// <summary>
        /// Create a component hub.
        /// </summary>
        /// <param name="httpServerContext">The server context. If null, a mock context will be created.</param>
        /// <returns>The component hub.</returns>
        public static ComponentHub CreateComponentHubMock(IHttpServerContext httpServerContext = null)
        {
            var ctorComponentHub = typeof(ComponentHub).GetConstructor
            (
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                null,
                [typeof(IHttpServerContext)],
                null
            );

            var componentHub = (ComponentHub)ctorComponentHub.Invoke
            ([
                httpServerContext ?? CreateHttpServerContextMock()
            ]);

            // set static field in the webex class
            var type = typeof(WebEx);
            var field = type.GetField("_componentHub", BindingFlags.Static | BindingFlags.NonPublic);

            field.SetValue(null, componentHub);

            return componentHub;
        }

        /// <summary>
        /// Create a component hub and register the plugins.
        /// </summary>
        /// <returns>The component hub.</returns>
        public static ComponentHub CreateAndRegisterComponentHubMock(IHttpServerContext httpServerContext = null)
        {
            var componentHub = CreateComponentHubMock(httpServerContext);
            var pluginManager = componentHub.PluginManager as PluginManager;

            pluginManager.Register();

            return componentHub;
        }

        /// <summary>
        /// Create a fake request.
        /// </summary>
        /// <param name="content">The content of the request.</param>
        /// <param name="uri">The URI of the request.</param>
        /// <returns>A fake request for testing.</returns>
        public static IRequest CreateRequestMock(string content = "", string uri = "")
        {
            var context = CreateHttpContextMock(content);

            var request = context.Request;

            if (!string.IsNullOrEmpty(uri))
            {
                request.Uri = new UriEndpoint(uri);
            }

            return request;
        }

        /// <summary>
        /// Create a fake request.
        /// </summary>
        /// <param name="uri">The URI of the request.</param>
        /// <returns>A fake request for testing.</returns>
        public static IRequest CreateRequestMock(IUri uri)
        {
            var context = CreateHttpContextMock();

            var request = context.Request;

            if (uri is not null)
            {
                request.Uri = uri as UriEndpoint;
            }

            return request;
        }

        /// <summary>
        /// Create a fake http context.
        /// </summary>
        /// <param name="content">The content.</param>
        /// <returns>A fake http context for testing.</returns>
        public static WebMessage.HttpContext CreateHttpContextMock(string content = "")
        {
            var ctorRequest = typeof(Request).GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, null, [typeof(IFeatureCollection), typeof(RequestHeaderFields), typeof(IHttpServerContext)], null);
            var featureCollection = new FeatureCollection();
            var firstLine = content.Split('\n').FirstOrDefault()?.TrimEnd('\r') ?? "";
            var lines = content.Split(_separator, StringSplitOptions.None);
            var filteredLines = lines.Skip(1).TakeWhile(line => !string.IsNullOrWhiteSpace(line));

            // locate the headers/body boundary in a line-ending-agnostic way: the
            // first occurrence of two consecutive line breaks (any combination of
            // \r\n, \n, \r) marks the end of the header section.
            var headerEnd = -1;
            var separatorLength = 0;
            foreach (var sep in new[] { "\r\n\r\n", "\n\n", "\r\r" })
            {
                var idx = content.IndexOf(sep, StringComparison.Ordinal);
                if (idx >= 0 && (headerEnd < 0 || idx < headerEnd))
                {
                    headerEnd = idx;
                    separatorLength = sep.Length;
                }
            }

            var innerContent = headerEnd >= 0 ? content[(headerEnd + separatorLength)..] : "";

            // HTTP wire format requires CRLF; normalize text-only bodies that
            // were checked out with LF only so the production multipart /
            // urlencoded parsers find their boundaries.
            if (innerContent.Length > 0 && !innerContent.Contains("\r\n"))
            {
                innerContent = innerContent.Replace("\n", "\r\n");
            }

            var contentBytes = Encoding.UTF8.GetBytes(innerContent);

            var requestFeature = new HttpRequestFeature
            {
                Headers = new HeaderDictionary
                {
                    ["Host"] = "localhost",
                    ["Connection"] = "keep-alive",
                    ["ContentType"] = "text/html",
                    ["ContentLength"] = innerContent.Length.ToString(),
                    ["ContentLanguage"] = "en",
                    ["ContentEncoding"] = "gzip, deflate, br, zstd",
                    ["Accept"] = "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7",
                    ["AcceptEncoding"] = "gzip, deflate, br, zstd",
                    ["AcceptLanguage"] = "de,de-DE;q=0.9,en;q=0.8,en-GB;q=0.7,en-US;q=0.6",
                    ["UserAgent"] = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36 Edg/126.0.0.0",
                    ["Referer"] = "0HN50661TV8TP",
                    ["Cookie"] = "session=AB333C76-E73F-45E0-85FD-123320D9B85F"
                },
                Body = contentBytes.Length > 0 ? new MemoryStream(contentBytes) : null,
                Method = firstLine.Split(' ')?.Where(x => !string.IsNullOrEmpty(x)).FirstOrDefault() ?? "GET",
                RawTarget = firstLine.Split(' ')?.Skip(1)?.FirstOrDefault()?.Split('?')?.FirstOrDefault() ?? "/",
                QueryString = "?" + firstLine.Split(' ')?.Skip(1)?.FirstOrDefault()?.Split('?')?.Skip(1)?.FirstOrDefault() ?? "",
            };

            foreach (var line in filteredLines)
            {
                // split at the first colon only: values such as an origin contain further ones
                var colon = line.IndexOf(':');
                var key = (colon < 0 ? line : line[..colon]).Trim();
                var value = colon < 0 ? "" : line[(colon + 1)..].Trim();
                requestFeature.Headers[key] = value;
            }

            requestFeature.Headers.ContentLength = contentBytes.Length;

            var requestIdentifierFeature = new HttpRequestIdentifierFeature
            {
                TraceIdentifier = "Ihr TraceIdentifier-Wert"
            };

            var connectionFeature = new HttpConnectionFeature
            {
                LocalPort = 8080,
                LocalIpAddress = IPAddress.Parse("192.168.0.1"),
                RemotePort = 8080,
                RemoteIpAddress = IPAddress.Parse("127.0.0.1"),
                ConnectionId = "0HN50661TV8TP"
            };

            featureCollection.Set<IHttpRequestFeature>(requestFeature);
            featureCollection.Set<IHttpRequestIdentifierFeature>(requestIdentifierFeature);
            featureCollection.Set<IHttpConnectionFeature>(connectionFeature);

            var context = new WebMessage.HttpContext(featureCollection, CreateHttpServerContextMock());

            return context;
        }

        /// <summary>
        /// Creates a mock render context for unit testing.
        /// </summary>
        /// <param name="applicationContext">The application context. If null, defaults to null.</param>
        /// <param name="scopes">The scopes of the page. If null, defaults to null.</param>
        /// <returns>A mock render context for testing.</returns>
        public static RenderContext CrerateRenderContextMock(IApplicationContext applicationContext = null, IEnumerable<Type> scopes = null)
        {
            var request = CreateRequestMock();

            return new RenderContext(null, CreratePageContextMock(applicationContext, scopes), request);
        }

        /// <summary>
        /// Create a fake page context for unit testing.
        /// </summary>
        /// <param name="scopes">The scopes of the page.</param>
        /// <returns>A fake context for testing.</returns>
        public static PageContext CreratePageContextMock(IApplicationContext applicationContext = null, IEnumerable<Type> scopes = null)
        {
            var ctorPageContext = typeof(PageContext).GetConstructor(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, [], null);

            var pageContext = (PageContext)ctorPageContext.Invoke([]);
            pageContext.ApplicationContext = applicationContext;
            pageContext.Scopes = scopes;

            return pageContext;
        }

        /// <summary>
        /// Gets the content of an embedded resource as a string.
        /// </summary>
        /// <param name="fileName">The name of the resource file.</param>
        /// <returns>The content of the embedded resource as a string.</returns>
        public static string GetEmbeddedResource(string fileName)
        {
            var assembly = typeof(UnitTestFixture).Assembly;
            var resources = assembly.GetManifestResourceNames();
            var resourceName = resources
                .FirstOrDefault(name => name.Replace('\\', '/').EndsWith(fileName.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase));

            using var stream = assembly.GetManifestResourceStream(resourceName);
            using var memoryStream = new MemoryStream();
            stream.CopyTo(memoryStream);
            var data = memoryStream.ToArray();

            return Encoding.UTF8.GetString(data);
        }

        /// <summary>
        /// Release of unmanaged resources reserved during use.
        /// </summary>
        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }
}
