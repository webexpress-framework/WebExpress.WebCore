using System.Collections.Generic;
using System.Globalization;
using System.Net;
using WebExpress.WebCore.WebApplication;
using WebExpress.WebCore.WebEndpoint;
using WebExpress.WebCore.WebParameter;
using WebExpress.WebCore.WebSession.Model;
using WebExpress.WebCore.WebUri;

namespace WebExpress.WebCore.WebMessage
{
    /// <summary>
    /// Defines the contract for accessing HTTP request information (see RFC 2616).
    /// </summary>
    public interface IRequest
    {
        /// <summary>
        /// Gets the context of the web server.
        /// </summary>
        IHttpServerContext HttpServerContext { get; }

        /// <summary>
        /// Gets the application context associated with the current component.
        /// </summary>
        IApplicationContext ApplicationContext { get; }

        /// <summary>
        /// Gets the context information associated with the current endpoint.
        /// </summary>
        IEndpointContext EndpointContext { get; }

        /// <summary>
        /// Gets the request method (e.g. POST).
        /// </summary>
        RequestMethod Method { get; }

        /// <summary>
        /// Gets the uri.
        /// </summary>
        UriEndpoint Uri { get; internal set; }

        /// <summary>
        /// Gets the session.
        /// </summary>
        Session Session { get; }

        /// <summary>
        /// Gets the http version.
        /// </summary>
        string Protocol { get; }

        /// <summary>
        /// Gets the options from the header.
        /// </summary>
        RequestHeaderFields Header { get; }

        /// <summary>
        /// Gets the ip address and port number of the server to which the request is made.
        /// </summary>
        EndPoint LocalEndPoint { get; }

        /// <summary>
        /// Gets the ip address and port number of the client from which the request originated.
        /// </summary>
        EndPoint RemoteEndPoint { get; }

        /// <summary>
        /// Gets a boolean value that indicates whether the tcp connection used to send the request uses the secure sockets layer (ssl) protocol.
        /// </summary>
        bool IsSecureConnection { get; }

        /// <summary>
        /// Gets the schema. This can be http or https.
        /// </summary>
        UriScheme Scheme { get; }

        /// <summary>
        /// Gets the request identifier of the incoming http request.
        /// </summary>
        string RequestTraceIdentifier { get; }

        /// <summary>
        /// Gets the culture.
        /// </summary>
        CultureInfo Culture { get; }

        /// <summary>
        /// Gets the collection of parameters associated with the request.
        /// </summary>
        IEnumerable<IParameter> Parameters { get; }

        /// <summary>
        /// Adds several parameters.
        /// </summary>
        /// <param name="param">The parameters.</param>
        void AddParameter(IEnumerable<Parameter> param);

        /// <summary>
        /// Adds one parameter.
        /// </summary>
        /// <param name="param">The parameter.</param>
        void AddParameter(Parameter param);

        /// <summary>
        /// Returns a parameter by name.
        /// </summary>
        /// <param name="name">The name of the parameter.</param>
        /// <returns>The value.</returns>
        IParameter GetParameter(string name);

        /// <summary>
        /// Returns a parameter by type.
        /// </summary>
        /// <typeparam name="TParameter">The parameter.</typeparam>
        /// <returns>The value.</returns>
        TParameter GetParameter<TParameter>()
            where TParameter : IParameterStatic, new();

        /// <summary>
        /// Checks whether a parameter exists.
        /// </summary>
        /// <param name="name">The name of the parameter.</param>
        /// <returns>True if the parameter is present, false otherwise.</returns>
        bool HasParameter(string name);
    }
}