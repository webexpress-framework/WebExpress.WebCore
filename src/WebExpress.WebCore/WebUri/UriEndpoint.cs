using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using WebExpress.WebCore.WebIcon;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebPage;
using WebExpress.WebCore.WebParameter;

namespace WebExpress.WebCore.WebUri
{
    /// <summary>
    /// An Uri represents a complete, fully qualified Uniform Resource 
    /// Identifier (URI) that uniquely identifies a endpoint (see RFC 3986).
    /// This interface encapsulates all components of a typical URI, such as 
    /// the scheme (e.g., "http", "https"), the authority (e.g., "example.com"), 
    /// path segments, query parameters, and fragment. It provides the external
    /// address used for resource identification and linking 
    /// (e.g., "http://example.com/users/123").
    /// </summary>
    public partial class UriEndpoint : IUri
    {
        /// <summary>
        /// A regular expression to match URIs.
        /// </summary>
        /// <returns>A Regex object for matching URIs.</returns>
        [GeneratedRegex("^([a-zA-Z0-9+.-]+):(?://(?:((?:[a-zA-Z0-9-._~!$&'()*+,;=:]|%[0-9a-fA-F]{2})*)@)?((?:[a-zA-Z0-9-._~!$&'()*+,;=]|%[0-9a-fA-F]{2})*)(?::(\\d*))?(.*)?)$")]
        private static partial Regex UriRegex();

        /// <summary>
        /// Regular expression to match relative URIs.
        /// </summary>
        /// <returns>A Regex object for matching relative URIs.</returns>
        [GeneratedRegex(@"^(\/([a-zA-Z0-9-+*%()=._/$]*))?(\?([a-zA-Z0-9-+*%()=._/$&]*))?(#([a-zA-Z0-9-+*%()=._/$]*))?$")]
        private static partial Regex RelativeUriRegex();

        /// <summary>
        /// Cached scheme tokens used to cheaply pre-filter whether a string looks like an
        /// absolute uri before running the more expensive absolute-uri regular expression.
        /// </summary>
        private static readonly string[] SchemeNames = Enum.GetNames<UriScheme>();

        /// <summary>
        /// The scheme (e.g. Http, FTP).
        /// </summary>
        public UriScheme Scheme { get; set; } = UriScheme.Http;

        /// <summary>
        /// The authority (e.g. user@example.com:8080).
        /// </summary>
        public UriAuthority Authority { get; set; }

        /// <summary>
        /// The path (e.g. /over/there).
        /// </summary>
        public IEnumerable<IUriPathSegment> PathSegments { get; private set; } = [new UriPathSegmentRoot()];

        /// <summary>
        /// Gets or sets the base path of the endpoint's URI.
        /// The base path is included only when the endpoint class has the IncludeSubPaths attribute enabled.
        /// For example, if the complete URI is "http://example.com/server/app/endpoint/extended",
        /// the <c>BasePath</c> property will represent the "http://example.com/server/app/endpoint" portion of the URI.
        /// </summary>
        /// <value>
        /// The base path as an <see cref="IUri"/> object, or <c>null</c> if the IncludeSubPaths attribute is not enabled.
        /// </value>
        public IUri BasePath { get; set; }

        /// <summary>
        /// The query part (e.g. ?title=Uniform_Resource_Identifier).
        /// </summary>
        public IEnumerable<IUriQuery> Query { get; private set; } = [];

        /// <summary>
        /// References a position within a resource (e.g. #Anchor).
        /// </summary>
        public string Fragment { get; set; }

        /// <summary>
        /// Determines if the uri is empty.
        /// </summary>
        public bool Empty => !PathSegments.Any();

        /// <summary>
        /// Determines if the Uri is the root.
        /// </summary>
        public bool IsRoot => PathSegments.Count() == 1;

        /// <summary>
        /// Checks if it is a relative uri.
        /// </summary>
        public bool IsRelative => Authority is null;

        /// <summary>
        /// Retrieves a collection of variables represented as key-value pairs.
        /// </summary>
        public IDictionary<string, string> Parameters
        {
            get
            {
                var dic = new Dictionary<string, string>();

                foreach (var path in PathSegments)
                {
                    // only variable segments contribute parameters; a null/blank name cannot be a dictionary key
                    if (path is IUriPathSegmentVariable variable && !string.IsNullOrEmpty(variable.VariableName))
                    {
                        dic.TryAdd(variable.VariableName.ToLowerInvariant(), variable.Value);
                    }
                }

                return dic;
            }
        }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public UriEndpoint()
        {

        }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="scheme">The scheme (e.g. Http, FTP).</param>
        /// <param name="authority">The authority (e.g. user@example.com:8080).</param>
        /// <param name="uri">The uri.</param>
        public UriEndpoint(UriScheme scheme, UriAuthority authority, string uri)
            : this(uri)
        {
            Scheme = scheme;
            Authority = authority;
        }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="uri">The uri.</param>
        public UriEndpoint(string uri)
        {
            if (string.IsNullOrWhiteSpace(uri) || uri == "/")
            {
                return;
            }

            if (SchemeNames.Any(x => uri.StartsWith(x, StringComparison.OrdinalIgnoreCase)))
            {
                var match = UriRegex().Match(uri);

                try
                {
                    Scheme = Enum.Parse<UriScheme>(match.Groups[1].Value, true);
                }
                catch
                {
                    Scheme = UriScheme.Http;
                }

                Authority = new UriAuthority()
                {
                    User = match.Groups[2].Success ? match.Groups[2].Value : null,
                    Host = match.Groups[3].Success ? match.Groups[3].Value : null,
                    // an empty port group (e.g. "host:/") must not throw a format exception
                    Port = int.TryParse(match.Groups[4].Value, out var port) ? port : null
                };

                uri = match.Groups[5].Value;
            }

            var relativeMatch = RelativeUriRegex().Match(uri);

            var segments = new List<IUriPathSegment>(PathSegments);
            foreach (var p in relativeMatch.Groups[2].Value.Split('/', StringSplitOptions.RemoveEmptyEntries))
            {
                segments.Add(new UriPathSegmentConstant(p));
            }
            PathSegments = segments;

            if (relativeMatch.Groups[4].Success)
            {
                var query = new List<IUriQuery>();
                foreach (var q in relativeMatch.Groups[4].Value.Split('&'))
                {
                    // split on the first '=' only so values may legitimately contain '='
                    var item = q.Split('=', 2);
                    query.Add(new UriQuery(item[0], item.Length > 1 ? item[1] : null));
                }
                Query = query;
            }

            Fragment = relativeMatch.Groups[6].Success ? relativeMatch.Groups[6].Value : null;
        }

        /// <summary>
        /// Copy constructor
        /// </summary>
        /// <param name="uri">The uri.</param>
        public UriEndpoint(IUri uri)
        {
            Scheme = uri?.Scheme ?? UriScheme.Http;
            Authority = uri?.Authority;
            PathSegments = [.. uri?.PathSegments.Select(x => x.Copy()) ?? []];
            Query = [.. uri?.Query.Select(x => new UriQuery(x.Key, x.Value)) ?? []];
            Fragment = uri?.Fragment;
        }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="segments">The path segments.</param>
        public UriEndpoint(params IUriPathSegment[] segments)
        {
            // PathSegments is already seeded with the single root; only non-root segments are appended
            PathSegments = [.. PathSegments.Concat(segments?.Where(x => x is not UriPathSegmentRoot) ?? [])];
        }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="uri">The uri.</param>
        /// <param name="segments">The path segments.</param>
        public UriEndpoint(IUri uri, IEnumerable<IUriPathSegment> segments)
            : this(uri.Scheme, uri.Authority, uri.Fragment, uri.Query, segments)
        {
        }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="uri">The uri.</param>
        /// <param name="segments">The path segments.</param>
        /// <param name="extendedSegments">Other segments.</param>
        public UriEndpoint(IUri uri, IEnumerable<IUriPathSegment> segments, IEnumerable<IUriPathSegment> extendedSegments)
            : this(uri.Scheme, uri.Authority, uri.Fragment, uri.Query, extendedSegments is not null
                  ? segments.Union(extendedSegments)
                  : segments)
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
        public UriEndpoint(UriScheme scheme, UriAuthority authority, string fragment, IEnumerable<IUriQuery> query, IEnumerable<IUriPathSegment> segments)
        {
            Scheme = scheme;
            Authority = authority;
            PathSegments = [.. PathSegments.Concat(segments?.Where(x => x is not UriPathSegmentRoot).Select(x => x.Copy()) ?? [])];
            Query = [.. query?.Select(x => new UriQuery(x.Key, x.Value)) ?? []];
            Fragment = fragment;
        }

        /// <summary>
        /// Appends one or more query parameters to the current URI and returns a new instance with 
        /// the updated query
        /// string.
        /// </summary>
        /// <param name="query">An array of objects representing the query parameters to add. Each 
        /// parameter must not be null.</param>
        /// <returns>The current instance for method chaining.</returns>
        public virtual IUri Add(params IUriQuery[] query)
        {
            Query = [.. Query.Concat(query?.Where(x => x is not null) ?? [])];

            return this;
        }

        /// <summary>
        /// Concatenates the given path segment to the current URI and returns a new instance of IUri with the updated path.
        /// </summary>
        /// <param name="segment">The path segment to be concatenated with the existing URI.</param>
        /// <returns>A new IUri instance representing the URI after concatenation.</returns>
        public virtual IUri Concat(string segment)
        {
            if (string.IsNullOrWhiteSpace(segment))
            {
                return this;
            }

            var copy = new UriEndpoint((IUri)this);
            copy.PathSegments = [.. copy.PathSegments
                .Concat(segment.Split('/', StringSplitOptions.RemoveEmptyEntries)
                .Select(x => new UriPathSegmentConstant(x)))];

            return copy;
        }

        /// <summary>
        /// Concatenates the given path segment to the current URI and returns a new instance of IUri with the updated path.
        /// </summary>
        /// <param name="segments">An array of path segments to be concatenated to the existing URI.</param>
        /// <returns>A new IUri instance representing the URI after concatenation.</returns>
        public virtual IUri Concat(params IUriPathSegment[] segments)
        {
            if (segments is null || segments.Length == 0)
            {
                return this;
            }

            // the copy constructor already deep-copies the existing segments, so they are not re-copied here
            var copy = new UriEndpoint((IUri)this);
            copy.PathSegments = [.. copy.PathSegments
                .Concat(segments.Where(x => x is not null && !x.IsEmpty))];

            return copy;
        }

        /// <summary>
        /// Appends one or more query segments and returns a new URI instance with the 
        /// combined query parameters.
        /// </summary>
        /// <param name="query">
        /// An array representing the query segments to append. The order of segments
        /// determines their position in the resulting query string.
        /// </param>
        /// <returns>
        /// A new uri instance containing the original URI with the specified query segments appended.
        /// </returns>
        public IUri Concat(params IUriQuery[] query)
        {
            var copy = new UriEndpoint((IUri)this);
            copy.Query = [.. copy.Query.Concat(query?.Where(x => x is not null) ?? [])];

            return copy;
        }

        /// <summary>
        /// Return a shortened uri containing n-elements.
        /// count greater than 0 count elements are included
        /// count less than 0 count elements are truncated
        /// count = 0 an empty uri is returned
        /// </summary>
        /// <param name="count">The count of elements to include or truncate.</param>
        /// <returns>The sub uri with the specified number of elements.</returns>
        public virtual IUri Take(int count)
        {
            if (count == 0)
            {
                return new UriEndpoint();
            }

            var copy = new UriEndpoint((IUri)this);
            var path = copy.PathSegments.ToList();

            if (count > 0)
            {
                copy.PathSegments = [.. path.Take(count)];
            }
            else if (Math.Abs(count) < path.Count)
            {
                copy.PathSegments = [.. path.Take(path.Count + count)];
            }
            else
            {
                return null;
            }

            return copy;
        }

        /// <summary>
        /// Returns a new URI containing the last <paramref name="count"/> path segments.
        /// </summary>
        /// <param name="count">
        /// The number of trailing path segments to include.  
        /// 
        /// <para>
        /// • If <paramref name="count"/> is 0, the full URI is returned.  
        /// • If <paramref name="count"/> is positive, the last <paramref name="count"/> segments are returned.  
        /// • If <paramref name="count"/> exceeds the number of segments, the full URI is returned.  
        /// • Negative values are not allowed and result in <c>null</c>.
        /// </para>
        /// </param>
        /// <returns>
        /// A new URI containing the selected trailing segments, or <c>null</c> if
        /// <paramref name="count"/> is negative.
        /// </returns>
        public virtual IUri TakeLast(int count)
        {
            var copy = new UriEndpoint((IUri)this);
            var path = copy.PathSegments.ToList();

            // negative values → return full URI
            if (count < 0)
            {
                return copy;
            }

            // 0 or count >= total → full URI
            if (count is 0 || count >= path.Count)
            {
                return copy;
            }

            // take last n segments
            copy.PathSegments = [.. path.Skip(path.Count - count)];

            return copy;
        }

        /// <summary>
        /// Return a shortened uri by not including the first n elements.
        /// count greater than 0 count elements are skipped
        /// count less than or equals 0 an empty Uri is returned
        /// </summary>
        /// <param name="count">The count of elements to skip.</param>
        /// <returns>The sub uri after skipping the specified number of elements.</returns>
        public IUri Skip(int count)
        {
            if (count >= PathSegments.Count())
            {
                return null;
            }

            var copy = new UriEndpoint((IUri)this);
            if (count > 0)
            {
                copy.PathSegments = [.. copy.PathSegments.Skip(count)];
            }

            return copy;
        }

        /// <summary>
        /// Determines whether the given segment is part of the uri.
        /// </summary>
        /// <param name="segment">The segment to be tested.</param>
        /// <returns>true if successful, false otherwise.</returns>
        public virtual bool Contains(string segment)
        {
            return PathSegments.Any(x => x.Value.Equals(segment, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Checks whether a given uri is part of that uri.
        /// </summary>
        /// <param name="uri">The Uri to be checked.</param>
        /// <returns>true if part of the uri, false otherwise.</returns>
        public bool StartsWith(IUri uri)
        {
            // materialize once to avoid repeated enumeration (Count/ElementAt would be O(n²))
            var a = uri.PathSegments as IReadOnlyList<IUriPathSegment> ?? uri.PathSegments.ToList();
            var b = PathSegments as IReadOnlyList<IUriPathSegment> ?? PathSegments.ToList();

            if (a.Count > b.Count)
            {
                return false;
            }

            for (int i = 0; i < a.Count; i++)
            {
                if (!a[i].Value.Equals(b[i].Value, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Creates a new endpoint uri and fills it with the given parameters.
        /// </summary>
        /// <param name="parameters">
        /// The parameters that fill in the variable parts of the uri.
        /// </param>
        /// <returns>
        /// A new endpoint uri with the populated parameters.
        /// </returns>
        public virtual IUri BindParameters(params IParameter[] parameters)
        {
            var pathSegments = PathSegments.AsEnumerable();

            foreach (var parameter in parameters ?? [])
            {
                var key = parameter switch
                {
                    IParameterStatic staticParam => staticParam.GetKey(),
                    IParameterDynamic dynamicParam => dynamicParam.Key,
                    _ => null
                };

                pathSegments = pathSegments.Select(x =>
                {
                    if (x is IUriPathSegmentVariable variable &&
                        variable.VariableName.Equals(key, StringComparison.OrdinalIgnoreCase))
                    {
                        var copy = variable.Copy() as IUriPathSegmentVariable;
                        copy.Value = parameter.Value;

                        return copy;
                    }

                    return x;
                });
            }

            // copy query collection and bind matching parameters by query keys
            var boundQuery = new List<IUriQuery>();
            foreach (var query in Query)
            {
                var parameter = (parameters ?? [])
                    .Select(x =>
                    {
                        var key = x switch
                        {
                            IParameterStatic staticParam => staticParam.GetKey(),
                            IParameterDynamic dynamicParam => dynamicParam.Key,
                            _ => null
                        };
                        return (key, x.Value);
                    })
                    .FirstOrDefault(x => string.Equals(x.key, query?.Key, StringComparison.InvariantCultureIgnoreCase));

                // keep the uri's declared query key; only its value is replaced when a parameter matches
                boundQuery.Add(string.IsNullOrWhiteSpace(parameter.key)
                    ? new UriQuery(query.Key, query.Value)
                    : new UriQuery(query.Key, parameter.Value));
            }

            return new UriEndpoint(this, [.. pathSegments])
            {
                Query = boundQuery
            };
        }

        /// <summary>
        /// Creates a new endpoint uri and fills it with the given parameters.
        /// </summary>
        /// <param name="parameters">
        /// The parameters that fill in the variable parts of the uri.
        /// </param>
        /// <returns>
        /// A new endpoint uri with the populated parameters.
        /// </returns>
        public virtual IUri BindParameters(IEnumerable<IParameter> parameters)
        {
            return BindParameters([.. parameters]);
        }

        /// <summary>
        /// Binds the parameters from the specified request to a URI instance.
        /// </summary>
        /// <param name="request">
        /// The request object containing the parameters to be bound to the URI. Cannot be null.
        /// </param>
        /// <returns>
        /// An new IUri instance that represents the URI with parameters bound from the request.
        /// </returns>
        public virtual IUri BindParameters(IRequest request)
        {
            return BindParameters([.. request?.Parameters]);
        }

        /// <summary>
        /// Combines the specified uris into a compound uri.
        /// </summary>
        /// <param name="uris">The uris to be combine.</param>
        /// <returns>A combined uri.</returns>
        public static IUri Combine(params string[] uris)
        {
            var copy = new UriEndpoint();

            copy.PathSegments = [.. copy.PathSegments
                .Concat(uris.Where(x => !string.IsNullOrWhiteSpace(x))
                .SelectMany(x => x.Split('/', StringSplitOptions.RemoveEmptyEntries))
                .Select(x => new UriPathSegmentConstant(x) as IUriPathSegment))];

            return copy;
        }

        /// <summary>
        /// Combines the specified uris into a compound uri.
        /// </summary>
        /// <param name="uris">The uris to be combine.</param>
        /// <returns>A combined uri.</returns>
        public static IUri Combine(params IUri[] uris)
        {
            var copy = new UriEndpoint(uris.FirstOrDefault());
            copy.PathSegments = [.. copy.PathSegments
                .Concat(uris.Skip(1).Where(x => x is not null).SelectMany(x => x.PathSegments.Skip(1)))];

            return copy;
        }

        /// <summary>
        /// Combines the specified uris into a compound uri.
        /// </summary>
        /// <param name="uri">The first uri to be combine.</param>
        /// <param name="uris">The uris to be combine.</param>
        /// <returns>A combined uri.</returns>
        public static IUri Combine(IUri uri, params string[] uris)
        {
            var copy = new UriEndpoint(uri);
            copy.PathSegments = [.. copy.PathSegments
                .Concat(uris.Where(x => !string.IsNullOrWhiteSpace(x))
                .SelectMany(x => x.Split('/', StringSplitOptions.RemoveEmptyEntries))
                .Select(x => new UriPathSegmentConstant(x) as IUriPathSegment))];

            return copy;
        }

        /// <summary>
        /// Sets the fragment component of the URI.
        /// </summary>
        /// <param name="fragment">The fragment to set (e.g., "section1").</param>
        /// <returns>A new IUri instance with the updated fragment. The original URI remains unchanged.</returns>
        public IUri SetFragment(string fragment)
        {
            return new UriEndpoint(Scheme, Authority, fragment, Query, PathSegments);
        }

        /// <summary>
        /// Converts a resource uri to a normal uri.
        /// </summary>
        /// <param name="uri">The uri to convert.</param>
        public static implicit operator string(UriEndpoint uri)
        {
            return uri?.ToString();
        }

        /// <summary>
        /// Returns a string that represents the display text for the current instance.
        /// </summary>
        /// <param name="renderContext">The render context.</param>
        /// <returns>
        /// A string containing the display text associated with the instance. The 
        /// value may be empty if no display text is available.
        /// </returns>
        public virtual string GetDisplayText(IRenderContext renderContext)
        {
            var last = PathSegments.LastOrDefault();

            if (last is IUriPathSegmentVariable variable)
            {
                return variable.GetDisplayText(renderContext);
            }

            return null;
        }

        /// <summary>
        /// Returns an icon that visually represents the parameter within the given render context.
        /// </summary>
        /// <param name="renderContext">
        /// The rendering context that provides information required to determine the appropriate icon.
        /// </param>
        /// <returns>
        /// An icon associated with the current instance. The value may be <c>null</c> or empty 
        /// if no icon is available.
        /// </returns>
        public virtual IIcon GetIcon(IRenderContext renderContext)
        {
            var last = PathSegments.LastOrDefault();

            if (last is IUriPathSegmentVariable variable)
            {
                return variable.GetIcon(renderContext);
            }

            return null;
        }

        /// <summary>
        /// Converts the uri to a string.
        /// </summary>
        /// <returns>A string that represents the current uri.</returns>
        public override string ToString()
        {
            var builder = new StringBuilder();

            if (Scheme != UriScheme.Mailto && !IsRelative)
            {
                builder.Append(Scheme.ToSchemeString());
                builder.Append(':');
                Authority?.AppendTo(builder, Scheme.DefaultPort());
            }
            else if (Scheme == UriScheme.Mailto)
            {
                builder.Append(Scheme.ToSchemeString());
                builder.Append(':');
                Authority?.AppendTo(builder, Scheme.DefaultPort());
                return builder.ToString();
            }

            var hasPathSegment = false;
            foreach (var segment in PathSegments)
            {
                if (segment is UriPathSegmentRoot)
                {
                    continue;
                }

                builder.Append('/');
                var value = segment?.ToString();
                if (!string.IsNullOrEmpty(value))
                {
                    builder.Append(value.TrimStart('/'));
                }

                hasPathSegment = true;
            }

            if (hasPathSegment)
            {
                while (builder.Length > 1 && builder[builder.Length - 1] == '/')
                {
                    builder.Length--;
                }
            }
            else
            {
                builder.Append('/');
            }

            var hasQuery = false;
            foreach (var query in Query)
            {
                builder.Append(hasQuery ? '&' : '?');
                hasQuery = true;
                builder.Append(query.Key);
                builder.Append('=');
                builder.Append(query.Value);
            }

            if (!string.IsNullOrWhiteSpace(Fragment))
            {
                builder.Append('#');
                builder.Append(Fragment);
            }

            return builder.ToString();
        }
    }
}