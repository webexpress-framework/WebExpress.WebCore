using System;
using System.Collections.Generic;
using System.Linq;
using WebExpress.WebCore.WebParameter;
using WebExpress.WebCore.WebUri;

namespace WebExpress.WebCore.WebEndpoint
{
    /// <summary>
    /// A route in WebExpress represents a logical internal path or pattern used to map endpoints within the sitemap.
    /// 
    /// While a URI (Uniform Resource Identifier) defines the complete, standardized external identifier of a resource
    /// (e.g., http://example.com/path?query=value#fragment) and thus represents the external address of an endpoint,
    /// a route represents the internal address of an endpoint that is used exclusively for internal purposes such as routing 
    /// and request processing.
    /// 
    /// A route contains neither a scheme, nor an authority, nor other external components. Furthermore, it may include 
    /// placeholders for dynamic segments (e.g., /users/{id}).
    /// </summary>
    public partial class RouteEndpoint : IRoute
    {
        private IUriPathSegment[] _pathSegments = [new UriPathSegmentRoot()];

        /// <summary>
        /// The segments of the route (e.g., /over/there).
        /// </summary>
        /// <remarks>
        /// The segments are taken in once, when the route is built. A route is built from other
        /// routes - copied, concatenated, combined - and kept as a deferred query it would copy
        /// every segment of every route it derives from again on each read, which every
        /// comparison and string conversion of a route is.
        /// </remarks>
        public IEnumerable<IUriPathSegment> PathSegments
        {
            get => _pathSegments;
            private set => _pathSegments = [.. value];
        }

        /// <summary>
        /// Gets or sets a string representation of the route.
        /// </summary>
        public string Display { get; set; }

        /// <summary>
        /// Determines if the route is the root.
        /// </summary>
        public bool IsRoot => PathSegments.Count() == 1;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public RouteEndpoint()
        {
        }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="scheme">The scheme (e.g. Http, FTP).</param>
        /// <param name="authority">The authority (e.g. user@example.com:8080).</param>
        /// <param name="uri">The uri.</param>
        public RouteEndpoint(UriScheme scheme, UriAuthority authority, string uri)
        {
        }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="route">The route.</param>
        public RouteEndpoint(string route)
        {
            if (string.IsNullOrWhiteSpace(route) || route == "/") return;

            PathSegments = PathSegments
                .Concat(route.Split('/', StringSplitOptions.RemoveEmptyEntries)
                .Select(x => new UriPathSegmentConstant(x)));
        }

        /// <summary>
        /// Copy constructor
        /// </summary>
        /// <param name="route">The route.</param>
        public RouteEndpoint(IRoute route)
        {
            PathSegments = route?.PathSegments.Select(x => x.Copy()) ?? [];
        }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="segments">The path segments.</param>
        public RouteEndpoint(params IUriPathSegment[] segments)
        {
            if (segments.Length > 0)
            {
                PathSegments = PathSegments
                    .Concat(segments.Where(x => !x.IsEmpty).Select(x => x.Copy()));
            }
        }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="segments">The path segments.</param>
        public RouteEndpoint(IEnumerable<IUriPathSegment> segments)
        {
            if (segments.Any())
            {
                PathSegments = PathSegments
                    .Concat(segments.Where(x => !x.IsEmpty).Select(x => x.Copy()));
            }
        }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="route">The base route.</param>
        /// <param name="segments">The path segments.</param>
        public RouteEndpoint(IRoute route, params string[] segments)
        {
        }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="route">The base route.</param>
        /// <param name="segments">The path segments.</param>
        public RouteEndpoint(IRoute route, params IUriPathSegment[] segments)
        {
        }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="route">The route.</param>
        /// <param name="segments">The path segments.</param>
        /// <param name="extendedSegments">Other segments.</param>
        public RouteEndpoint(IRoute route, IEnumerable<IUriPathSegment> segments, IEnumerable<IUriPathSegment> extendedSegments)
        {
        }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="scheme">The scheme (e.g. Http, FTP).</param>
        /// <param name="authority">The authority (e.g. user@example.com:8080).</param>
        /// <param name="fragment">References a position within a resource (e.g. #Anchor).</param>
        /// <param name="query">The query part (e.g. ?title=Uniform_Resource_Identifier).</param>
        /// <param name="segments">The path segments.</param>
        public RouteEndpoint(UriScheme scheme, UriAuthority authority, string fragment, IEnumerable<UriQuery> query, IEnumerable<IUriPathSegment> segments)
        {
        }

        /// <summary>
        /// Concatenates the given path segment to the current route and returns a new instance of IRoute with the updated path.
        /// </summary>
        /// <param name="segment">The path segment to be concatenated with the existing route.</param>
        /// <returns>A new IRoute instance representing the route after concatenation.</returns>
        public IRoute Concat(string segment)
        {
            if (string.IsNullOrWhiteSpace(segment))
            {
                return this;
            }

            var copy = new RouteEndpoint((IRoute)this);
            copy.PathSegments = copy.PathSegments
                .Concat(segment.Split('/', StringSplitOptions.RemoveEmptyEntries)
                .Select(x => new UriPathSegmentConstant(x)));

            return copy;
        }

        /// <summary>
        /// Concatenates the given path segment to the current route and returns a new instance of IRoute with the updated path.
        /// </summary>
        /// <param name="segments">An array of path segments to be concatenated to the existing route.</param>
        /// <returns>A new IRoute instance representing the route after concatenation.</returns>
        public virtual IRoute Concat(params IUriPathSegment[] segments)
        {
            if (segments is null || segments.Length == 0)
            {
                return this;
            }

            var copy = new RouteEndpoint((IRoute)this);
            copy.PathSegments = copy.PathSegments
                .Select(x => x.Copy())
                .Concat(segments.Where(x => x is not null).Where(x => !x.IsEmpty));

            return copy;
        }

        /// <summary>
        /// Removes a specified segment from the route and returns a new instance of IRoute with the updated path.
        /// </summary>
        /// <param name="segments">The path segment to be removed from the existing route.</param>
        /// <returns>A new IRoute instance representing the route after the segment removal.</returns>
        public virtual IRoute RemoveSegment(string segments)
        {
            if (string.IsNullOrWhiteSpace(segments) || !ToString().Contains(segments))
            {
                return this;
            }

            var segmentParts = segments.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var copy = new RouteEndpoint((IRoute)this);

            copy.PathSegments = copy.PathSegments
                .Where(x => !segmentParts.Contains(x.Value))
                .Select(x => x.Copy());

            return copy;
        }

        /// <summary>
        /// Converts the route to a URI.
        /// </summary>
        /// <param name="parameters">The parameters to be included in the URI.</param>
        /// <returns>An instance of IUri representing the route as a URI.</returns>
        public IUri ToUri(params Parameter[] parameters)
        {
            return new UriEndpoint([.. PathSegments]).BindParameters(parameters);
        }

        /// <summary>
        /// Combines the specified routes into a compound route.
        /// </summary>
        /// <param name="routes">The routes to be combine.</param>
        /// <returns>A combined route.</returns>
        public static RouteEndpoint Combine(params IRoute[] routes)
        {
            var r = routes.Skip(1).Where(x => !x.IsRoot).SelectMany(x => x.PathSegments.Skip(1));
            var copy = new RouteEndpoint(routes.FirstOrDefault());
            copy.PathSegments = copy.PathSegments
                .Concat(r);

            return copy;
        }

        /// <summary>
        /// Combines the specified routes into a compound uri.
        /// </summary>
        /// <param name="baseRoute">The base route to be used as the starting point.</param>
        /// <param name="routes">The routes to be combine.</param>
        /// <returns>A combined route.</returns>
        public static RouteEndpoint Combine(IRoute baseRoute, params string[] routes)
        {
            var copy = new RouteEndpoint(baseRoute);
            copy.PathSegments = copy.PathSegments
                .Concat(routes.Where
                (
                    x => !string.IsNullOrWhiteSpace(x))
                        .SelectMany(x => x.Split('/', StringSplitOptions.RemoveEmptyEntries)
                )
                .Select(x => new UriPathSegmentConstant(x) as IUriPathSegment));

            return copy;
        }

        /// <summary>
        /// Combines the specified base route, intermediate route, and enumerable segments into a compound route.
        /// </summary>
        /// <param name="baseRoute">The base route serving as the starting point.</param>
        /// <param name="segments">The enumerable collection of path segments to be added.</param>
        /// <returns>A new compound route combining all specified components.</returns>
        public static RouteEndpoint Combine(IRoute baseRoute, IEnumerable<IUriPathSegment> segments)
        {
            var copy = new RouteEndpoint(baseRoute);
            copy.PathSegments = copy.PathSegments
                .Concat(segments);

            return copy;
        }

        /// <summary>
        /// Combines the specified base route, intermediate route, and enumerable segments into a compound route.
        /// </summary>
        /// <param name="baseRoute">The base route serving as the starting point.</param>
        /// <param name="segments">The path segment(s) to be added.</param>
        /// <returns>A new compound route combining all specified components.</returns>
        public static RouteEndpoint Combine(IRoute baseRoute, string segments)
        {
            var copy = new RouteEndpoint(baseRoute);
            copy.PathSegments = copy.PathSegments
                .Concat
                (
                    segments?.Split('/', StringSplitOptions.RemoveEmptyEntries)
                        ?.Select(x => new UriPathSegmentConstant(x)) ?? []
                );

            return copy;
        }

        /// <summary>
        /// Converts a route to a string.
        /// </summary>
        /// <param name="route">The uri to convert.</param>
        public static implicit operator string(RouteEndpoint route)
        {
            return route?.ToString();
        }

        /// <summary>
        /// Converts the route to a string.
        /// </summary>
        /// <returns>A string that represents the current route.</returns>
        public override string ToString()
        {
            var path = "/" + string.Join
            (
                "/",
                PathSegments.Where(x => x is not UriPathSegmentRoot)
                    .Select(x => x.ToString().TrimStart('/'))
            );

            return path?.TrimEnd('/');
        }
    }
}