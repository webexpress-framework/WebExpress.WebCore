using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Transport.Sockets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebCore.WebEndpoint;
using WebExpress.WebCore.WebLog;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebPage;
using WebExpress.WebCore.WebParameter;
using WebExpress.WebCore.WebSetting;
using WebExpress.WebCore.WebSitemap;
using WebExpress.WebCore.WebSocket;
using WebExpress.WebCore.WebStatusPage;
using WebExpress.WebCore.WebUri;

namespace WebExpress.WebCore
{
    /// <summary>
    /// The web server for processing http requests (see RFC 2616). The web server uses Kestrel internally.
    /// </summary>
    public class HttpServer : IHost, IHttpApplication<IHttpContext>
    {
        private static readonly IComponentHub _componentHub = WebEx.ComponentHub;

        /// <summary>
        /// Event is triggered after the web server is started.
        /// </summary>
        public event EventHandler Started;

        /// <summary>
        /// Provides the KestrelServer, which responds to the requests.
        /// </summary>
        private KestrelServer Kestrel { get; set; }

        /// <summary>
        /// Gets the server thread termination.
        /// </summary>
        private CancellationTokenSource ServerTokenSource { get; } = new CancellationTokenSource();

        /// <summary>
        /// Gets or sets the settings of the server. Left unset, every value keeps its built-in default.
        /// </summary>
        public HttpServerSettings Settings { get; set; }

        /// <summary>
        /// Gets the context.
        /// </summary>
        public IHttpServerContext HttpServerContext { get; protected set; }

        /// <summary>
        /// Gets or sets the culture.
        /// </summary>
        public CultureInfo Culture { get; set; }

        /// <summary>
        /// Gets the execution time of the web server.
        /// </summary>
        public static DateTime ExecutionTime { get; } = DateTime.Now;

        /// <summary>
        /// Gets the request number;
        /// </summary>
        public long RequestNumber { get; private set; }

        /// <summary>
        /// Gets the statistics history.
        /// </summary>
        public static List<HttpServerStatisticItem> Statistics { get; } = [];

        /// <summary>
        /// Synchronization object for statistics.
        /// </summary>
        private static readonly Lock _statLock = new();

        // Variables for CPU usage calculation
        private static readonly Process _currentProcess = Process.GetCurrentProcess();
        private static DateTime _lastCpuTime = DateTime.UtcNow;
        private static TimeSpan _lastProcessorTime = _currentProcess.TotalProcessorTime;
        private static readonly Lock _cpuStatLock = new();

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="context">The server context.</param>
        public HttpServer(IHttpServerContext context)
        {
            HttpServerContext = new HttpServerContext
            (
                context.Route,
                context.Endpoints,
                context.PackagePath,
                context.AssetPath,
                context.DataPath,
                context.SettingsPath,
                context.Configuration,
                context.Culture,
                context.Log,
                this
            );

            Culture = HttpServerContext.Culture;
        }

        /// <summary>
        /// Starts the HTTP(S) server.
        /// </summary>
        public void Start()
        {
            if (HttpServerContext is not null && HttpServerContext.Log != null)
            {
                HttpServerContext.Log?.Info(message: I18N.Translate("webexpress.webcore:httpserver.run"));
            }

            if (!HttpListener.IsSupported)
            {
                HttpServerContext.Log?.Error(message: I18N.Translate("webexpress.webcore:httpserver.notsupported"));
            }

            var logger = new LogFactory();
            var transportOptions = new OptionsWrapper<SocketTransportOptions>
            (
                new SocketTransportOptions()
            );
            var transport = new SocketTransportFactory(transportOptions, logger);
            var serviceCollection = new ServiceCollection();

            serviceCollection.AddMemoryCache();
            serviceCollection.AddLogging(x =>
            {
                x.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Trace);
                x.AddProvider(logger);
            });
            serviceCollection.AddHttpLogging
            (
                x =>
                {
                    x.LoggingFields = Microsoft.AspNetCore.HttpLogging.HttpLoggingFields.All;
                }
            );

            // the kestrel settings block is optional; a missing block or property keeps the built-in defaults
            var kestrel = Settings?.Kestrel;

            var serverOptions = new OptionsWrapper<KestrelServerOptions>(new KestrelServerOptions()
            {
                AllowSynchronousIO = kestrel?.AllowSynchronousIO ?? true,
                AllowResponseHeaderCompression = kestrel?.AllowResponseHeaderCompression ?? true,
                AddServerHeader = kestrel?.AddServerHeader ?? true,
                ApplicationServices = serviceCollection.BuildServiceProvider()
            });

            var limits = serverOptions.Value.Limits;

            if (kestrel?.MaxConcurrentConnections is not null)
            {
                limits.MaxConcurrentConnections = kestrel.MaxConcurrentConnections;
            }
            if (kestrel?.MaxRequestBodySize is not null)
            {
                limits.MaxRequestBodySize = kestrel.MaxRequestBodySize;
            }
            if (kestrel?.MaxRequestHeadersTotalSize is not null)
            {
                limits.MaxRequestHeadersTotalSize = kestrel.MaxRequestHeadersTotalSize.Value;
            }
            if (kestrel?.MaxConcurrentUpgradedConnections is not null)
            {
                limits.MaxConcurrentUpgradedConnections = kestrel.MaxConcurrentUpgradedConnections;
            }
            if (kestrel?.MaxRequestBufferSize is not null)
            {
                limits.MaxRequestBufferSize = kestrel.MaxRequestBufferSize;
            }
            if (kestrel?.MaxResponseBufferSize is not null)
            {
                limits.MaxResponseBufferSize = kestrel.MaxResponseBufferSize;
            }
            if (kestrel?.MaxRequestLineSize is not null)
            {
                limits.MaxRequestLineSize = kestrel.MaxRequestLineSize.Value;
            }
            if (kestrel?.KeepAliveTimeout is not null)
            {
                limits.KeepAliveTimeout = TimeSpan.FromSeconds(kestrel.KeepAliveTimeout.Value);
            }
            if (kestrel?.RequestHeadersTimeout is not null)
            {
                limits.RequestHeadersTimeout = TimeSpan.FromSeconds(kestrel.RequestHeadersTimeout.Value);
            }

            var protocols = kestrel?.ResolveProtocols();

            foreach (var endpoint in Settings?.Endpoints ?? [])
            {
                AddEndpoint(serverOptions, endpoint, protocols);
            }

            Kestrel = new KestrelServer(serverOptions, transport, logger);
            Kestrel.StartAsync(this, ServerTokenSource.Token);

            HttpServerContext.Log?.Info(message: I18N.Translate
            (
                "webexpress.webcore:httpserver.start"),
                args: [ExecutionTime.ToShortDateString(), ExecutionTime.ToLongTimeString()]
            );

            Started?.Invoke(this, new EventArgs());
        }

        /// <summary>
        /// Adds an endpoint.
        /// </summary>
        /// <param name="serverOptions">The server options.</param>
        /// <param name="endPoint">The endpoint.</param>
        /// <param name="protocols">The HTTP protocols to enable on the endpoint, or null to keep the Kestrel default.</param>
        private void AddEndpoint(OptionsWrapper<KestrelServerOptions> serverOptions, EndpointSettings endPoint, HttpProtocols? protocols)
        {
            try
            {
                var uri = new UriBuilder(endPoint.Uri);
                var asterisk = uri.Host.Equals("*");

                var port = uri.Port;
                var host = asterisk ? Dns.GetHostEntry(Dns.GetHostName()) : Dns.GetHostEntry(uri.Host);
                var addressList = host.AddressList
                    .Union(asterisk ? Dns.GetHostEntry("localhost").AddressList : [])
                    .Where(x => x.AddressFamily == AddressFamily.InterNetwork || x.AddressFamily == AddressFamily.InterNetworkV6);

                HttpServerContext.Log?.Info(message: I18N.Translate("webexpress.webcore:httpserver.endpoint"), args: endPoint.Uri);

                foreach (var ipAddress in addressList)
                {
                    var ep = new IPEndPoint(ipAddress, port);

                    switch (uri.Scheme)
                    {
                        case "HTTPS":
                            {
                                AddEndpoint(serverOptions, ep, endPoint.PfxFile, endPoint.Password, protocols);
                                break;
                            }
                        default:
                            {
                                AddEndpoint(serverOptions, ep, protocols);
                                break;
                            }
                    }
                }
            }
            catch (Exception ex)
            {
                HttpServerContext.Log?.Error(message: I18N.Translate("webexpress.webcore:httpserver.listen.exeption"), args: endPoint);
                HttpServerContext.Log?.Exception(ex);
            }
        }

        /// <summary>
        /// Adds an endpoint.
        /// </summary>
        /// <param name="serverOptions">The server options.</param>
        /// <param name="endPoint">The endpoint.</param>
        /// <param name="protocols">The HTTP protocols to enable on the endpoint, or null to keep the Kestrel default.</param>
        private void AddEndpoint(OptionsWrapper<KestrelServerOptions> serverOptions, IPEndPoint endPoint, HttpProtocols? protocols)
        {
            serverOptions.Value.Listen(endPoint, configure =>
            {
                if (protocols is not null)
                {
                    configure.Protocols = protocols.Value;
                }
            });
            HttpServerContext.Log?.Info(message: I18N.Translate("webexpress.webcore:httpserver.listen"), args: endPoint.ToString());
        }

        /// <summary>
        /// Adds an endpoint with HTTPS configuration.
        /// </summary>
        /// <param name="serverOptions">The server options.</param>
        /// <param name="endPoint">The endpoint.</param>
        /// <param name="pfxFile">The path to the PFX file containing the certificate.</param>
        /// <param name="password">The password for the PFX file.</param>
        /// <param name="protocols">The HTTP protocols to enable on the endpoint, or null to keep the Kestrel default.</param>
        private void AddEndpoint(OptionsWrapper<KestrelServerOptions> serverOptions, IPEndPoint endPoint, string pfxFile, string password, HttpProtocols? protocols)
        {
            serverOptions.Value.Listen(endPoint, configure =>
            {
                var cert = X509CertificateLoader.LoadPkcs12FromFile(pfxFile, password, X509KeyStorageFlags.DefaultKeySet);
                configure.UseHttps(cert);

                if (protocols is not null)
                {
                    configure.Protocols = protocols.Value;
                }
            });

            HttpServerContext.Log?.Info(message: I18N.Translate("webexpress.webcore:httpserver.listen"), args: endPoint.ToString());
        }

        /// <summary>
        /// Stops the HTTP(S) server.
        /// </summary>
        public void Stop()
        {
            // signal cancellation and stop server
            ServerTokenSource.Cancel();
            Kestrel.StopAsync(ServerTokenSource.Token);
        }

        /// <summary>
        /// Handles an incoming request.
        /// </summary>
        /// <param name="context">The context of the web request.</param>
        /// <param name="searchResult">The previously resolved search result for the request.</param>
        /// <returns>The response to be sent back to the caller.</returns>
        private IResponse HandleClient(IHttpContext context, SearchResult searchResult)
        {
            var stopwatch = Stopwatch.StartNew();
            var request = context.Request;
            var response = default(IResponse);

            HttpServerContext.Log?.Debug(message: I18N.Translate("webexpress.webcore:httpserver.connected"), args: context.RemoteEndPoint);
            HttpServerContext.Log?.Info(I18N.Translate
            (
                "webexpress.webcore:httpserver.request",
                context.RemoteEndPoint,
                ++RequestNumber,
                $"{request?.Method} {request?.Uri} {request?.Protocoll}"
            ));

            var resourceUri = new UriEndpoint(request.Uri, searchResult.Uri.PathSegments)
            {
                BasePath = searchResult.Uri.BasePath
            };
            request.Uri = resourceUri;

            // the request is made ambient for as long as it is being answered, so a layer that
            // is several calls away from the endpoint - a manager, a store - can still ask what
            // it is being asked on behalf of. It is closed with the response, so nothing reads
            // it afterwards
            using var current = WebEx.BeginRequest(request);

            try
            {
                // execute resource
                request.AddParameter(searchResult.Uri.Parameters.Select(x => new Parameter(x.Key, x.Value, ParameterScope.Url)));

                if (searchResult.EndpointContext is not null)
                {
                    response = WebEx.ComponentHub.EndpointManager.HandleRequest(request, searchResult.EndpointContext);

                    if (response is ResponseNotFound)
                    {
                        response = CreateStatusPage<ResponseNotFound>
                        (
                            string.Empty,
                            request,
                            searchResult
                        );
                    }
                }
                else
                {
                    // resource not found
                    response = CreateStatusPage<ResponseNotFound>
                    (
                        "Resource not found",
                        request,
                        searchResult
                    );
                }
            }
            catch (RedirectException ex)
            {
                if (ex.Permanet)
                {
                    response = new ResponseMovedPermanently(ex.Uri);
                }
                else
                {
                    response = new ResponseMovedTemporarily(ex.Uri);
                }
            }
            catch (BadRequestException ex)
            {
                var message = $"<h4>Message</h4>{ex.Message}<br/><br/>" +
                        $"<h5>Source</h5>{ex.Source}<br/><br/>" +
                        $"<h5>StackTrace</h5>{ex.StackTrace.Replace("\n", "<br/>\n")}";

                response = CreateStatusPage<ResponseBadRequest>
                (
                    message,
                    request,
                    searchResult
                );
            }
            catch (Exception ex)
            {
                if (ex is TargetInvocationException tie && tie.InnerException is RedirectException rex)
                {
                    response = rex.Permanet
                        ? new ResponseMovedPermanently(rex.Uri)
                        : new ResponseMovedTemporarily(rex.Uri);
                }
                else
                {
                    HttpServerContext.Log?.Exception(ex);

                    var message = Describe(ex);

                    response = CreateStatusPage<ResponseInternalServerError>
                    (
                        message,
                        request,
                        searchResult
                    );
                }
            }

            stopwatch.Stop();

            HttpServerContext.Log?.Info(I18N.Translate
            (
                "webexpress.webcore:httpserver.request.done",
                context?.RemoteEndPoint,
                RequestNumber,
                stopwatch.ElapsedMilliseconds,
                response.Status
            ));

            return response;
        }

        /// <summary>
        /// Hands the client the id of its session whenever the cookie it sent does not name it.
        /// </summary>
        /// <remarks>
        /// That is the case on a first visit, for a stale or forged cookie the session manager
        /// refused to adopt, and after a sign-in or sign-out replaced the id. It is applied to
        /// every response - a redirect or a status page included - because a sign-in that ends
        /// in a redirect would otherwise leave the browser with an id that no longer resolves,
        /// and the user signed out again.
        ///
        /// The cookie carries the security attributes that match what the id is worth. It is
        /// http-only, because the id is the whole of what authenticates the client and script on
        /// the page - injected or not - must not be able to read it. It is secure whenever the
        /// request arrived over https (or the configuration forces it, for a server behind a
        /// tls-terminating proxy), so the id is never sent back in the clear. Its lifetime is
        /// bounded by the session timeout rather than set to never expire, so a copy of the
        /// cookie stops working once the session it names has lapsed. The path is named rather
        /// than left to the browser, which would default it to the directory of the request the
        /// cookie was handed out on: a visitor would then collect one session per directory they
        /// touch, the sign-in would bind the identity to whichever of them the login request
        /// happened to carry, and every page under a different directory would be served to a
        /// session that never signed in.
        /// </remarks>
        /// <param name="request">The request that was answered.</param>
        /// <param name="response">The response about to be sent.</param>
        private void IssueSessionCookie(IRequest request, IResponse response)
        {
            var session = request?.Session;
            var cookies = response?.Header?.Cookies;

            if (session is null || cookies is null)
            {
                return;
            }

            // a handler that set the cookie itself knows better
            if (cookies.Any(x => x.Name.Equals("session", StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            var sent = request.Header?.Cookies?
                .FirstOrDefault(x => x.Name.Equals("session", StringComparison.OrdinalIgnoreCase));

            if (Guid.TryParse(sent?.Value, out var sentId) && sentId == session.Id)
            {
                return;
            }

            // secure tracks the request scheme by default; a deployment behind a tls proxy that
            // sees plain http can force it on through configuration
            var secure = Settings?.Session?.Secure ?? request.Scheme == UriScheme.Https;

            var cookie = new Cookie("session", session.Id.ToString())
            {
                Path = "/",
                HttpOnly = true,
                Secure = secure
            };

            // a positive timeout bounds the cookie to the session's idle window; with expiry
            // disabled the cookie has no Expires at all and so dies when the browser closes,
            // which is still bounded, unlike a cookie that never expires
            var timeout = WebEx.ComponentHub?.SessionManager?.Timeout ?? TimeSpan.Zero;
            if (timeout > TimeSpan.Zero)
            {
                cookie.Expires = DateTime.Now + timeout;
            }

            cookies.Add(cookie);
        }

        /// <summary>
        /// Updates the request statistics with ring buffer logic (max 24h).
        /// </summary>
        /// <param name="response">The response containing the status code.</param>
        /// <param name="duration">The duration of the request in milliseconds.</param>
        private static void UpdateStatistics(IResponse response, long duration)
        {
            var now = DateTime.Now;
            var minute = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0);
            var isError = response is not null && response.Status >= 400;

            // calculate memory usage in MB
            var memUsage = _currentProcess.WorkingSet64 / (1024.0 * 1024.0);

            // calculate cpu usage (read & update protected by _cpuStatLock)
            var currentCpuTime = _currentProcess.TotalProcessorTime;
            var currentWallTime = DateTime.UtcNow;
            var cpuUsage = 0.0;

            lock (_cpuStatLock)
            {
                var cpuUsedMs = (currentCpuTime - _lastProcessorTime).TotalMilliseconds;
                var totalMsPassed = (currentWallTime - _lastCpuTime).TotalMilliseconds;

                if (totalMsPassed > 0)
                {
                    cpuUsage = (cpuUsedMs / (totalMsPassed * Environment.ProcessorCount)) * 100.0;
                }

                // update pointers for next calculation
                _lastProcessorTime = currentCpuTime;
                _lastCpuTime = currentWallTime;
            }

            lock (_statLock)
            {
                // remove entries older than 24 hours (1440 minutes)
                while (Statistics.Count >= 1440)
                {
                    Statistics.RemoveAt(0);
                }

                var current = Statistics.LastOrDefault();

                if (current is not null && current.Timestamp == minute)
                {
                    current.Requests++;
                    if (isError)
                    {
                        current.Errors++;
                    }

                    // update min, max and total duration
                    if (duration < current.MinDuration)
                    {
                        current.MinDuration = duration;
                    }
                    if (duration > current.MaxDuration)
                    {
                        current.MaxDuration = duration;
                    }
                    current.TotalDuration += duration;

                    // calculate moving average for system metrics within this minute
                    current.CpuUsage += (cpuUsage - current.CpuUsage) / current.Requests;
                    current.MemoryUsage += (memUsage - current.MemoryUsage) / current.Requests;
                }
                else
                {
                    Statistics.Add(new HttpServerStatisticItem()
                    {
                        Timestamp = minute,
                        Requests = 1,
                        Errors = isError ? 1 : 0,
                        MinDuration = duration,
                        MaxDuration = duration,
                        TotalDuration = duration,
                        CpuUsage = cpuUsage,
                        MemoryUsage = memUsage
                    });
                }
            }
        }

        /// <summary>
        /// Creates a status page.
        /// </summary>
        /// <param name="message">The error message.</param>
        /// <param name="request">The request.</param>
        /// <param name="searchResult">The plugin by searching the status page or null.</param>
        /// <returns>The response.</returns>
        private static IResponse CreateStatusPage<TResponse>(string message, IRequest request, SearchResult searchResult = null)
            where TResponse : Response, new()
        {
            var response = new TResponse() as Response;

            if (IsNonHtmlFileRequest(request))
            {
                return CreatePlainStatusResponse(response);
            }

            var statusPageManager = WebEx.ComponentHub.StatusPageManager;
            var applicationManager = WebEx.ComponentHub.ApplicationManager;

            // a request that failed before its context could be built has none, and the status
            // page still has to be produced - it is the only place the original failure is
            // reported. Without the guard the report itself fails and the caller receives an
            // empty answer naming nothing.
            var route = request?.Uri?.PathSegments is null
                ? null
                : new RouteEndpoint(request.Uri.PathSegments)?.ToString();
            var applicationContext = string.IsNullOrEmpty(route)
                ? null
                : applicationManager.Applications
                    .FirstOrDefault(x => route.StartsWith(x.Route.ToString()));

            if (searchResult is not null)
            {
                return statusPageManager.CreateStatusResponse
                (
                    message,
                    response.Status,
                    searchResult?.EndpointContext?.ApplicationContext,
                    request
                );
            }

            if (applicationContext is not null)
            {
                return statusPageManager.CreateStatusResponse
                (
                    message,
                    response.Status,
                    applicationContext,
                    request
                );
            }

            message = $"<html><head><title>{response.Status}</title></head><body>" +
                      $"<p>{message}<br/><p>" +
                      $"</body></html>";

            response.Content = message;
            response.Header.ContentLength = message.Length;
            response.Header.ContentType = "text/html; charset=utf-8";

            return response;
        }

        /// <summary>
        /// Determines whether the request names a file whose type is not html. A browser
        /// loading a stylesheet, script or image does not surface the status of the answer,
        /// it only reads the body, so an html status page is taken as the file itself: a
        /// missing stylesheet presents as a valid one with no rules and a missing script as
        /// one that defines nothing.
        /// </summary>
        /// <param name="request">The request whose target is examined.</param>
        /// <returns>True when the requested file is of a known, non-html type.</returns>
        private static bool IsNonHtmlFileRequest(IRequest request)
        {
            var file = request?.Uri?.PathSegments?.LastOrDefault()?.ToString();
            var contentType = ContentTypeExtensions.ToContentType(System.IO.Path.GetExtension(file));

            return contentType != ContentType.Unknown
                && contentType != ContentType.Html
                && contentType != ContentType.Htm;
        }

        /// <summary>
        /// Answers a status for a non-html file with a plain text body, so a browser rejects
        /// it instead of accepting the status page as the file it asked for. The status
        /// itself is untouched; only the body a client would otherwise misread is replaced.
        /// </summary>
        /// <param name="response">The status response to complete.</param>
        /// <returns>The response carrying a plain text body.</returns>
        private static IResponse CreatePlainStatusResponse(Response response)
        {
            var content = $"{response.Status} - {response.Reason}";

            response.Content = content;
            response.Header.ContentLength = content.Length;
            response.Header.ContentType = "text/plain; charset=utf-8";

            return response;
        }

        /// <summary>
        /// Creates an appropriate IHttpContext instance (HttpContext or WebSocketContext) 
        /// based on feature detection.
        /// </summary>
        /// <param name="contextFeatures">The feature collection of the request.</param>
        /// <returns>An IHttpContext instance for the request.</returns>
        public IHttpContext CreateContext(IFeatureCollection contextFeatures)
        {
            try
            {
                var requestFeature = contextFeatures.Get<IHttpRequestFeature>();

                // check if schema or upgrade header indicates websocket
                if (IsWebSocketRequest(requestFeature))
                {
                    // use WebSocketContext for websocket connections
                    return new HttpWebSocketContext(contextFeatures, HttpServerContext);
                }

                // use regular HttpContext for normal HTTP requests
                return new HttpContext(contextFeatures, HttpServerContext);
            }
            catch (Exception ex)
            {
                // fall back to HttpExceptionContext on error
                return new HttpExceptionContext(ex, contextFeatures);
            }
        }

        /// <summary>
        /// Processes an http context asynchronously and answers a failure the pipeline itself
        /// could not handle.
        /// </summary>
        /// <remarks>
        /// Kestrel treats an exception escaping here as a transport failure: it logs the bare
        /// message without a stack trace and closes the connection with an empty body. Every
        /// request then looks identically broken and nothing says where. Catching it means the
        /// cause is written to the server log once and the caller receives a status page it can
        /// read - which is what makes a fault in the shell diagnosable at all.
        /// </remarks>
        /// <param name="httpContext">The http context that the operation processes.</param>
        /// <returns>Provides an asynchronous operation that handles the http context.</returns>
        public async Task ProcessRequestAsync(IHttpContext httpContext)
        {
            try
            {
                await ProcessRequestCoreAsync(httpContext);
            }
            catch (Exception ex)
            {
                HttpServerContext.Log?.Exception(ex);

                var response = CreateStatusPage<ResponseInternalServerError>
                (
                    Describe(ex),
                    httpContext?.Request
                );

                await new ResponseSender().SendAsync(httpContext, response);
            }
        }

        /// <summary>
        /// Renders an exception as the html fragment a status page shows.
        /// </summary>
        /// <remarks>
        /// The stack trace is read defensively: an exception that was constructed but never
        /// thrown carries none, and reading it unguarded fails inside the very code that
        /// exists to report the first failure.
        /// </remarks>
        /// <param name="ex">The exception to describe.</param>
        /// <returns>The html fragment.</returns>
        private static string Describe(Exception ex)
        {
            return $"<h4>Message</h4>{ex.Message}<br/><br/>" +
                $"<h5>Source</h5>{ex.Source}<br/><br/>" +
                $"<h5>StackTrace</h5>{ex.StackTrace?.Replace("\n", "<br/>\n")}<br/><br/>" +
                $"<h5>InnerException</h5>{ex.InnerException?.ToString().Replace("\n", "<br/>\n")}";
        }

        /// <summary>
        /// Processes an http context asynchronously.
        /// If the request is a websocket upgrade to a configured endpoint, handle
        /// websocket lifecycle instead of request/response.
        /// Handles missing sitemap endpoints directly here.
        /// </summary>
        /// <param name="httpContext">The http context that the operation processes.</param>
        /// <returns>Provides an asynchronous operation that handles the http context.</returns>
        private async Task ProcessRequestCoreAsync(IHttpContext httpContext)
        {
            var sender = new ResponseSender();
            var stopwatch = Stopwatch.StartNew();

            // every response leaves through this local send, so the statistics are recorded
            // here rather than inside the handler: a request that never reaches a handler -
            // an unknown route, a denied or an unauthenticated one - produces a status code
            // the monitor has to account for just the same. Recording precedes the send, so
            // a slow client does not end up counted as a slow server. The session cookie is
            // issued here for the same reason: the login prompt and a redirect after sign-in
            // never reach the handler either, yet must carry the session id.
            async Task SendAsync(IHttpContext context, IResponse response)
            {
                IssueSessionCookie(context?.Request, response);
                UpdateStatistics(response, stopwatch.ElapsedMilliseconds);

                await sender.SendAsync(context, response);
            }

            if (httpContext is HttpExceptionContext exceptionContext)
            {
                var message = "<html><head><title>404</title></head><body>" +
                    Describe(exceptionContext.Exception) +
                    "</body></html>";

                var response500 = CreateStatusPage<ResponseInternalServerError>(message, httpContext?.Request);

                await SendAsync(exceptionContext, response500);

                return;
            }

            var culture = httpContext?.Request?.Culture;
            var searchResult = WebEx.ComponentHub.SitemapManager.SearchResource(httpContext?.Uri, new SearchContext()
            {
                Culture = culture,
                HttpContext = httpContext,
                HttpServerContext = HttpServerContext
            });

            if (searchResult is null || searchResult.EndpointContext is null)
            {
                var notFoundResponse = CreateStatusPage<ResponseNotFound>
                (
                    "Resource not found",
                    httpContext.Request
                );

                await SendAsync(httpContext, notFoundResponse);

                return;
            }

            var applicationContext = searchResult.EndpointContext.ApplicationContext;

            if (httpContext.Request is Request request)
            {
                request.ApplicationContext = applicationContext;
                request.EndpointContext = searchResult.EndpointContext;
            }

            if (httpContext is HttpWebSocketContext)
            {
                // try to obtain websocket context and optional handler
                var socketContext = searchResult.EndpointContext as ISocketContext;

                await HandleWebSocketAsync(httpContext, socketContext);

                return;
            }

            // no policies (null or empty) -> serve directly without an access check
            if (!(searchResult.EndpointContext.Policies?.Any() ?? false))
            {
                var response = HandleClient(httpContext, searchResult);
                await SendAsync(httpContext, response);

                return;
            }

            var identity = _componentHub?.IdentityManager.GetCurrentIdentity(httpContext.Request);

            // if access is granted
            if (_componentHub.IdentityManager.CheckAccess(identity, searchResult.EndpointContext))
            {
                var response = HandleClient(httpContext, searchResult);
                await SendAsync(httpContext, response);

                return;
            }

            // access is denied (the grant case returned above) - determine the appropriate response
            {
                // if the user is authenticated but lacks the required permissions, show the forbidden page
                if (identity is not null && searchResult.EndpointContext is IPageContext)
                {
                    var forbiddenResponse = _componentHub?.IdentityManager.CreateForbiddenResponse
                    (
                        httpContext.Request,
                        searchResult.EndpointContext as IPageContext,
                        identity
                    );

                    if (forbiddenResponse is not null)
                    {
                        await SendAsync(httpContext, forbiddenResponse);
                        return;
                    }
                    else
                    {
                        forbiddenResponse = CreateStatusPage<ResponseForbidden>
                        (
                            new StatusMessage("You do not have permission to access this resource.").Message,
                            httpContext.Request,
                            searchResult
                        );

                        await SendAsync(httpContext, forbiddenResponse);
                        return;
                    }
                }
                else if (identity is not null)
                {
                    var forbiddenResponse = new ResponseForbidden(new StatusMessage("You do not have permission to access this resource."));

                    await SendAsync(httpContext, forbiddenResponse);
                    return;
                }
                else if (searchResult.EndpointContext is IPageContext pageContext)
                {
                    // if the user is not authenticated, show the login prompt
                    var loginResponse = _componentHub?.IdentityManager.CreateAuthenticationPrompt
                    (
                        httpContext.Request,
                        searchResult.EndpointContext as IPageContext,
                        identity
                    );

                    if (loginResponse is not null)
                    {
                        await SendAsync(httpContext, loginResponse);
                        return;
                    }
                }
                else
                {
                    var unauthorizedResponse = new ResponseUnauthorized(new StatusMessage("Authentication required. Provide a valid access token."));

                    await SendAsync(httpContext, unauthorizedResponse);
                    return;
                }
            }

            // fallback: no specific denied-response (login prompt / forbidden) could be created
            {
                var response = HandleClient(httpContext, searchResult);
                await SendAsync(httpContext, response);
            }
        }

        /// <summary>
        /// Handles the complete WebSocket request lifecycle for the given HTTP context.
        /// Validates the upgrade request, delegates the connection handling to the socket manager,
        /// and returns appropriate HTTP error responses when the handshake or connection setup fails.
        /// </summary>
        /// <param name="httpContext">
        /// The current HTTP context containing the incoming WebSocket upgrade request.
        /// </param>
        /// <param name="socketContext">
        /// Optional WebSocket endpoint context resolved from the sitemap. May be <c>null</c>
        /// if the endpoint does not define additional metadata.
        /// </param>
        public async Task HandleWebSocketAsync(IHttpContext httpContext, ISocketContext socketContext)
        {
            var responseSender = new ResponseSender();
            var socketManager = WebEx.ComponentHub.SocketManager;

            // validate that the request is a websocket upgrade
            if (httpContext is not HttpWebSocketContext)
            {
                // websocket not requested by client; return 400 Bad Request
                await responseSender.SendAsync(httpContext, new ResponseBadRequest(new StatusMessage("WebSocket upgrade required")));

                return;
            }

            try
            {
                await socketManager.HandleConnectionAsync(httpContext, socketContext);
            }
            catch (SocketHandshakeException)
            {
                // missing or invalid websocket handshake headers -> respond with 426
                var response = new ResponseUpgradeRequired(new StatusMessage("Invalid WebSocket handshake headers"));
                response.Header.Upgrade = "websocket";

                await responseSender.SendAsync(httpContext, response);
            }
            catch (SocketMessageTooLargeException ex)
            {
                // the client sent a WebSocket message exceeding the configured maximum size -> respond with 413 
                var response = new ResponsePayloadTooLarge(new StatusMessage($"WebSocket message exceeds the maximum allowed size of {ex.MaxSize} bytes."));
                await responseSender.SendAsync(httpContext, response);
            }
            catch (SocketException ex)
            {
                HttpServerContext.Log?.Exception(ex);

                // return 500 when socket error 
                var response = new ResponseInternalServerError(new StatusMessage("A transport-level socket error occurred during WebSocket communication."));
                await responseSender.SendAsync(httpContext, response);
            }
            catch (Exception ex)
            {
                // log unhandled exceptions during websocket processing
                HttpServerContext.Log?.Exception(ex);

                // return 500 when handshake did not succeed and no websocket established
                var response = new ResponseInternalServerError(new StatusMessage("An unexpected server error occurred during WebSocket processing."));
                await responseSender.SendAsync(httpContext, response);
            }
        }

        /// <summary>
        /// Discard a specified http context.
        /// </summary>
        /// <param name="context">The http context to discard.</param>
        /// <param name="exception">The exception that is thrown if processing did not complete successfully; otherwise null.</param>
        public void DisposeContext(IHttpContext context, Exception exception)
        {
        }

        /// <summary>
        /// Checks whether the current request is a WebSocket connection.
        /// </summary>
        /// <param name="requestFeature">The HTTP request feature instance.</param>
        /// <returns>True if it is a WebSocket connection; otherwise, false.</returns>
        private static bool IsWebSocketRequest(IHttpRequestFeature requestFeature)
        {
            // check scheme and "Upgrade" header for websocket protocol
            if (requestFeature == null)
            {
                return false;
            }

            var upgradeHeader = requestFeature.Headers.Upgrade;
            var scheme = requestFeature.Scheme;
            var isWebSocket =
                upgradeHeader.Contains("websocket", StringComparer.OrdinalIgnoreCase) ||
                string.Equals(scheme, "ws", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(scheme, "wss", StringComparison.OrdinalIgnoreCase);

            return isWebSocket;
        }
    }
}