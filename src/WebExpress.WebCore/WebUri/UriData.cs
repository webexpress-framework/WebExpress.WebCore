using System;
using System.Linq;

namespace WebExpress.WebCore.WebUri
{
    /// <summary>
    /// A data url (RFC 2397), which carries its content instead of naming a location. It is kept
    /// as written: parsed as an endpoint it would lose everything but its scheme, and a picture
    /// sent inline would be served as an empty address.
    /// </summary>
    /// <remarks>
    /// The only part a data url shares with an endpoint is the fragment, which can address a
    /// part of the content (an element of an inline svg). Every other transformation of an
    /// endpoint - a path segment, a query, a shortened path - has no place in a data url, so it
    /// is refused instead of returning an endpoint without the content; one that adds nothing
    /// returns the data url itself.
    /// </remarks>
    public class UriData : UriEndpoint
    {
        private readonly string _content;

        /// <summary>
        /// Gets the data url as written, with the current fragment.
        /// </summary>
        public string Value => ToString();

        /// <summary>
        /// Gets a value that is always false, because a data url needs no base to be resolved
        /// against; read as relative, it would be prefixed with a path and lose its content.
        /// </summary>
        public override bool IsRelative => false;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="value">The data url, starting with "data:".</param>
        public UriData(string value)
        {
            // a '#' always starts the fragment; inside the content it has to be percent-encoded
            var hash = value?.IndexOf('#') ?? -1;

            _content = hash < 0 ? value : value[..hash];
            Fragment = hash < 0 ? null : value[(hash + 1)..];
        }

        /// <summary>
        /// Returns the data url itself, which has no variable parts to fill.
        /// </summary>
        /// <param name="parameters">The parameters, which are ignored.</param>
        /// <returns>This data url.</returns>
        public override IUri BindParameters(params WebParameter.IParameter[] parameters)
        {
            return this;
        }

        /// <summary>
        /// Returns a data url with the same content and the given fragment.
        /// </summary>
        /// <param name="fragment">The fragment, or null to remove it.</param>
        /// <returns>A new data url. This one remains unchanged.</returns>
        public override IUri SetFragment(string fragment)
        {
            return new UriData(_content) { Fragment = fragment };
        }

        /// <summary>
        /// Returns the data url itself for a blank segment; a data url has no path to extend.
        /// </summary>
        /// <param name="segment">The path segment.</param>
        /// <returns>This data url.</returns>
        /// <exception cref="NotSupportedException">The segment is not blank.</exception>
        public override IUri Concat(string segment)
        {
            return string.IsNullOrWhiteSpace(segment) ? this : throw Unsupported("a path");
        }

        /// <summary>
        /// Returns the data url itself when no segment adds anything; a data url has no path to extend.
        /// </summary>
        /// <param name="segments">The path segments.</param>
        /// <returns>This data url.</returns>
        /// <exception cref="NotSupportedException">A segment is not empty.</exception>
        public override IUri Concat(params IUriPathSegment[] segments)
        {
            return segments?.Any(x => x is not null && !x.IsEmpty) == true ? throw Unsupported("a path") : this;
        }

        /// <summary>
        /// Returns the data url itself when no query is given; everything after the comma of a
        /// data url is content, so a query would become part of it.
        /// </summary>
        /// <param name="query">The query parameters.</param>
        /// <returns>This data url.</returns>
        /// <exception cref="NotSupportedException">A query parameter is given.</exception>
        public override IUri Concat(params IUriQuery[] query)
        {
            return query?.Any(x => x is not null) == true ? throw Unsupported("a query") : this;
        }

        /// <summary>
        /// Refuses to shorten the path, which a data url does not have.
        /// </summary>
        /// <param name="count">The count of elements.</param>
        /// <returns>Never returns.</returns>
        /// <exception cref="NotSupportedException">Always.</exception>
        public override IUri Take(int count)
        {
            throw Unsupported("a path");
        }

        /// <summary>
        /// Refuses to shorten the path, which a data url does not have.
        /// </summary>
        /// <param name="count">The count of elements.</param>
        /// <returns>Never returns.</returns>
        /// <exception cref="NotSupportedException">Always.</exception>
        public override IUri TakeLast(int count)
        {
            throw Unsupported("a path");
        }

        /// <summary>
        /// Refuses to shorten the path, which a data url does not have.
        /// </summary>
        /// <param name="count">The count of elements.</param>
        /// <returns>Never returns.</returns>
        /// <exception cref="NotSupportedException">Always.</exception>
        public override IUri Skip(int count)
        {
            throw Unsupported("a path");
        }

        /// <summary>
        /// Returns the data url with its fragment.
        /// </summary>
        /// <returns>The data url.</returns>
        public override string ToString()
        {
            return string.IsNullOrEmpty(Fragment) ? _content : $"{_content}#{Fragment}";
        }

        /// <summary>
        /// Creates the exception for a transformation a data url cannot take.
        /// </summary>
        /// <param name="part">The part the transformation needs.</param>
        /// <returns>The exception.</returns>
        private static NotSupportedException Unsupported(string part)
        {
            return new NotSupportedException($"A data url has no {part}; its content would be lost.");
        }
    }
}
