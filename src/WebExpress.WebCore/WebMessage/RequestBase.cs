using Microsoft.AspNetCore.Http.Features;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using WebExpress.WebCore.WebApplication;
using WebExpress.WebCore.WebEndpoint;
using WebExpress.WebCore.WebParameter;
using WebExpress.WebCore.WebSession.Model;
using WebExpress.WebCore.WebUri;

namespace WebExpress.WebCore.WebMessage
{
    /// <summary>
    /// Common base class for incoming requests (see RFC 2616). It extracts and exposes the
    /// information shared by every request type — HTTP method, URI, header fields, client and
    /// server endpoints, session, culture, and the query and session parameters. Concrete
    /// requests such as <see cref="Request"/> and <see cref="RequestWebSocket"/> build on it.
    /// </summary>
    public abstract class RequestBase : IRequest
    {
        private readonly ParameterDictionary _param = [];
        private Session _session;

        internal Session ExistingSession { get => _session; set => _session = value; }
        internal string QueryString { get; private set; }

        /// <summary>
        /// Gets the context of the web server.
        /// </summary>
        public IHttpServerContext HttpServerContext { get; protected set; }

        /// <summary>
        /// Gets the application context associated with the current component.
        /// </summary>
        public IApplicationContext ApplicationContext { get; internal set; }

        /// <summary>
        /// Gets the context information associated with the current endpoint.
        /// </summary>
        public IEndpointContext EndpointContext { get; internal set; }

        /// <summary>
        /// Gets the request method (e.g. POST).
        /// </summary>
        public RequestMethod Method { get; private set; }

        /// <summary>
        /// Gets or sets the uri.
        /// </summary>
        public UriEndpoint Uri { get; set; }

        /// <summary>
        /// Creates optional application state only when a caller needs it; authentication never depends on it.
        /// </summary>
        public Session Session => _session ??= WebEx.ComponentHub?.SessionManager?.GetSession(this);

        /// <summary>
        /// Gets the http version.
        /// </summary>
        public string Protocoll { get; private set; }

        /// <summary>
        /// Gets the options from the header.
        /// </summary>
        public RequestHeaderFields Header { get; private set; }

        /// <summary>
        /// Gets the ip address and port number of the server to which the request is made.
        /// </summary>
        public EndPoint LocalEndPoint { get; private set; }

        /// <summary>
        /// Gets the ip address and port number of the client from which the request originated.
        /// </summary>
        public EndPoint RemoteEndPoint { get; private set; }

        /// <summary>
        /// Gets a boolean value that indicates whether the tcp connection used to send the request uses the secure sockets layer (ssl) protocol.
        /// </summary>
        public bool IsSecureConnection { get; private set; }

        /// <summary>
        /// Gets the shema. This can be http or https.
        /// </summary>
        public UriScheme Scheme { get; private set; }

        /// <summary>
        /// Gets the request identifier of the incoming http request.
        /// </summary>
        public string RequestTraceIdentifier { get; private set; }

        /// <summary>
        /// Gets the culture.
        /// </summary>
        public CultureInfo Culture
        {
            get
            {
                try
                {
                    // see RFC 5646 
                    var languages = Header?.AcceptLanguage.FirstOrDefault();
                    var language = languages?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();

                    return new CultureInfo(language);
                }
                catch
                {
                    return HttpServerContext.Culture ?? CultureInfo.CurrentCulture;
                }
            }
        }

        /// <summary>
        /// Gets the collection of parameters associated with the request.
        /// </summary>
        public IEnumerable<IParameter> Parameters => _param.Values;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="contextFeatures">Initial set of features.</param>
        /// <param name="header">The header.</param>
        /// <param name="httpServerContext">The context of the web server.</param>
        internal RequestBase(IFeatureCollection contextFeatures, RequestHeaderFields header, IHttpServerContext httpServerContext)
        {
            var connectionFeature = contextFeatures.Get<IHttpConnectionFeature>();
            var requestFeature = contextFeatures.Get<IHttpRequestFeature>();
            var requestIdentifierFeature = contextFeatures.Get<IHttpRequestIdentifierFeature>();
            //var sessionFeature = contextFeatures.Get<ISessionFeature>();

            HttpServerContext = httpServerContext;
            RequestTraceIdentifier = requestIdentifierFeature.TraceIdentifier;
            Protocoll = requestFeature.Protocol;

            Scheme = requestFeature.Scheme.ToLower() switch
            {
                "http" => UriScheme.Http,
                "https" => UriScheme.Https,
                "ftp" => UriScheme.FTP,
                "file" => UriScheme.File,
                "mailto" => UriScheme.Mailto,
                "ldap" => UriScheme.Ldap,
                _ => UriScheme.Http

            };
            Method = requestFeature.Method.ToUpper() switch
            {
                "GET" => RequestMethod.GET,
                "POST" => RequestMethod.POST,
                "PUT" => RequestMethod.PUT,
                "DELETE" => RequestMethod.DELETE,
                "HEAD" => RequestMethod.HEAD,
                "PATCH" => RequestMethod.PATCH,
                _ => RequestMethod.GET
            };

            Header = header;

            LocalEndPoint = new IPEndPoint(connectionFeature.LocalIpAddress, connectionFeature.LocalPort);
            RemoteEndPoint = new IPEndPoint(connectionFeature.RemoteIpAddress, connectionFeature.RemotePort);

            Uri = new UriEndpoint
            (
                Scheme,
                new UriAuthority()
                {
                    Host = Header.Host,
                    Port = connectionFeature.LocalPort
                },
                requestFeature.RawTarget
            );

            QueryString = requestFeature.QueryString;
            ParseQueryParams(QueryString);
            if (Header.Cookies.Any(x => x.Name == "session")) { ParseSessionParams(); }
        }

        /// <summary>
        /// Returns the parameters from the reuest query (for example, http://www.example.com?key=value).
        /// </summary>
        /// <param name="query">The query.</param>
        private void ParseQueryParams(string query)
        {
            query = query.TrimStart('?');

            Parallel.ForEach(query.Split('&'), (param) =>
            {
                if (!string.IsNullOrWhiteSpace(param))
                {
                    var split = param.Split('=');

                    if (split.Length == 1)
                    {
                        AddParameter(new Parameter(split[0], null, ParameterScope.Parameter));
                    }
                    else if (split.Length == 2)
                    {
                        AddParameter(new Parameter(split[0], split[1], ParameterScope.Parameter));
                    }
                    else if (split.Length > 2)
                    {
                        AddParameter(new Parameter(split[0], string.Join("=", split.Skip(1)), ParameterScope.Parameter));
                    }
                }
            });
        }

        /// <summary>
        /// Parse the session parameters.
        /// </summary>
        private void ParseSessionParams()
        {
            var property = Session?.GetProperty<SessionPropertyParameter>();
            if (property is not null && property.Params is not null)
            {
                foreach (var param in property.Params)
                {
                    AddParameter(new Parameter(param.Key?.ToLower(), param.Value.Value, ParameterScope.Session));
                }
            }
        }

        /// <summary>
        /// Adds several parameters.
        /// </summary>
        /// <param name="param">The parameters.</param>
        public void AddParameter(IEnumerable<Parameter> param)
        {
            foreach (var p in param)
            {
                AddParameter(p);
            }
        }

        /// <summary>
        /// Adds one parameter.
        /// </summary>
        /// <param name="param">The parameter.</param>
        public void AddParameter(Parameter param)
        {
            var key = param.Key.ToLower();

            if (!_param.TryAdd(key, param))
            {
                _param[key] = param;
            }
        }

        /// <summary>
        /// Returns a parameter by name.
        /// </summary>
        /// <param name="name">The name of the parameter.</param>
        /// <returns>The value.</returns>
        public IParameter GetParameter(string name)
        {
            if (!string.IsNullOrWhiteSpace(name) && HasParameter(name))
            {
                return _param[name.ToLower()];
            }

            return null;
        }

        /// <summary>
        /// Returns a parameter by name.
        /// </summary>
        /// <typeparam name="TParameter">The parameter.</typeparam>
        /// <returns>The value.</returns>
        public TParameter GetParameter<TParameter>()
            where TParameter : IParameterStatic, new()
        {
            var key = TParameter.Key;

            if (!string.IsNullOrWhiteSpace(key) && HasParameter(key))
            {
                var p = _param[key.ToLower()];

                var parameter = new TParameter
                {
                    Value = p.Value,
                    Scope = p.Scope
                };

                return parameter;
            }

            return default;
        }

        /// <summary>
        /// Checks whether a parameter exists.
        /// </summary>
        /// <param name="name">The name of the parameter.</param>
        /// <returns>True if parameters are present, false otherwise.</returns>
        public bool HasParameter(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            return _param.ContainsKey(name.ToLower());
        }
    }
}
