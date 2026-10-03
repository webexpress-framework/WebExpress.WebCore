using Microsoft.AspNetCore.Http.Features;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebCore.WebParameter;

namespace WebExpress.WebCore.WebMessage
{
    /// <summary>
    /// Represents a single incoming HTTP request (see RFC 2616). It wraps the raw request data
    /// provided by ASP.NET Core and makes it easy to consume: besides the common request
    /// information from <see cref="RequestBase"/> (method, URI, headers, session), it reads the
    /// request body and turns it into parameters, supporting URL-encoded forms, plain text, and
    /// multipart form data including file uploads.
    /// </summary>
    public partial class Request : RequestBase
    {
        [GeneratedRegex(@"([\w-]+)=(.*)")]
        private static partial Regex TextRegex();

        [GeneratedRegex(@"Content-Type:\s*(.*)", RegexOptions.IgnoreCase, "de-DE")]
        private static partial Regex ContentRegex();

        // matching whole tokens (name|filename) prevents the "name" parameter from being
        // confused with the trailing "name" inside "filename" regardless of their order.
        [GeneratedRegex(@"(?:^|[;\s])(name|filename)\s*=\s*""([^""]*)""", RegexOptions.IgnoreCase)]
        private static partial Regex DispositionParamRegex();

        /// <summary>
        /// The initial size of the request body buffer. Typical form posts fit without a resize,
        /// while larger bodies grow the buffer only as their bytes are received.
        /// </summary>
        internal const int InitialContentBufferSize = 64 * 1024;

        /// <summary>
        /// Gets the content.
        /// </summary>
        public byte[] Content { get; private set; }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="contextFeatures">Initial set of features.</param>
        /// <param name="header">The header.</param>
        /// <param name="httpServerContext">The context of the web server.</param>
        internal Request(IFeatureCollection contextFeatures, RequestHeaderFields header, IHttpServerContext httpServerContext)
            : base(contextFeatures, header, httpServerContext)
        {
            var requestFeature = contextFeatures.Get<IHttpRequestFeature>();

            Content = GetContent(requestFeature.Body, Header.ContentLength);

            ParseRequestParams();
        }

        /// <summary>
        /// Returns the content.
        /// </summary>
        /// <param name="body">The content of a request.</param>
        /// <param name="contentLength">The number of bytes sent in the body or zero.</param>
        /// <returns>The content as a byte array, or null if the body is empty.</returns>
        internal static byte[] GetContent(Stream body, long? contentLength)
        {
            if (!contentLength.HasValue || contentLength.Value <= 0)
            {
                return null;
            }

            // the announced length is client-controlled, so it only caps the read; memory is
            // committed as bytes actually arrive, otherwise a slow client announcing a large
            // body would pin that much memory per connection before sending anything
            var length = (int)Math.Min(contentLength.Value, int.MaxValue);
            var buffer = new byte[Math.Min(length, InitialContentBufferSize)];

            var offset = 0;
            while (offset < length)
            {
                if (offset == buffer.Length)
                {
                    Array.Resize(ref buffer, (int)Math.Min((long)buffer.Length * 2, length));
                }

                var read = body.Read(buffer, offset, buffer.Length - offset);
                if (read == 0)
                {
                    break;
                }

                offset += read;
            }

            if (offset == 0)
            {
                return null;
            }

            // the client announced more bytes than it actually sent; trim to what arrived
            return offset == length ? buffer : buffer[..offset];
        }

        /// <summary>
        /// Parse the request parameters.
        /// </summary>
        protected virtual void ParseRequestParams()
        {
            if (string.IsNullOrWhiteSpace(Header.ContentType) || Content is null || Content.Length == 0)
            {
                return;
            }

            // normalize content-type; the first segment is the media type, the
            // remaining segments carry parameters such as the multipart boundary.
            var ct = Header.ContentType.Split(';', StringSplitOptions.TrimEntries);
            var enctype = TypeEnctypeExtensions.Convert(ct[0].ToLowerInvariant());

            switch (enctype)
            {
                case TypeEnctype.Multipart:
                    ParseMultipart(ct);
                    break;

                case TypeEnctype.Text:
                    ParseTextPlain();
                    break;

                case TypeEnctype.UrLEncoded:
                    ParseUrlEncoded();
                    break;

                default:
                    // unknown or unsupported content-type
                    break;
            }
        }

        /// <summary>
        /// Parses multipart form data from the provided content type parts and extracts parameters and 
        /// file uploads.
        /// </summary>
        /// <param name="contentTypeParts">
        /// An array of strings representing the parts of the Content-Type header. Each part may include 
        /// information such as the boundary used to separate multipart sections.
        /// </param>
        private void ParseMultipart(string[] contentTypeParts)
        {
            // extract boundary
            var boundary = contentTypeParts
                .FirstOrDefault(x => x.StartsWith("boundary=", StringComparison.OrdinalIgnoreCase))
                ?["boundary=".Length..];

            if (string.IsNullOrWhiteSpace(boundary))
            {
                return;
            }

            var boundaryBytes = Encoding.UTF8.GetBytes("--" + boundary);
            var endBoundaryBytes = Encoding.UTF8.GetBytes("--" + boundary + "--");

            int pos = 0;

            while (true)
            {
                // find next boundary
                int start = IndexOf(Content, boundaryBytes, pos);
                if (start < 0)
                {
                    break;
                }

                // check for end boundary
                bool isFinal = StartsWith(Content, endBoundaryBytes, start);

                // move to header start
                int headerStart = start + boundaryBytes.Length + GetLineBreakLength(Content, start + boundaryBytes.Length);

                // find header end (empty line)
                int headerSeparatorLength;
                int headerEnd = FindHeaderEnd(Content, headerStart, out headerSeparatorLength);
                if (headerEnd < 0)
                {
                    break;
                }

                var headerText = Encoding.UTF8.GetString(Content, headerStart, headerEnd - headerStart);

                // parse the content-disposition parameters in a single pass
                var name = string.Empty;
                var filename = string.Empty;
                foreach (Match dispo in DispositionParamRegex().Matches(headerText))
                {
                    if (string.Equals(dispo.Groups[1].Value, "filename", StringComparison.OrdinalIgnoreCase))
                    {
                        filename = dispo.Groups[2].Value;
                    }
                    else
                    {
                        name = dispo.Groups[2].Value;
                    }
                }

                var contentType = ExtractContentType(headerText);

                // content start
                int dataStart = headerEnd + headerSeparatorLength;

                // find next boundary to determine data length
                int nextBoundary = IndexOf(Content, boundaryBytes, dataStart);
                if (nextBoundary < 0)
                {
                    break;
                }

                int dataLength = nextBoundary - dataStart;
                dataLength -= GetTrailingLineBreakLength(Content, nextBoundary);
                if (dataLength < 0)
                {
                    // Defensive fallback for malformed parts where boundary follows unexpectedly early.
                    dataLength = 0;
                }

                if (string.IsNullOrEmpty(filename))
                {
                    // normal field
                    var value = Encoding.UTF8.GetString(Content, dataStart, dataLength).TrimEnd();
                    AddParameter(new Parameter(name, value, ParameterScope.Parameter));
                }
                else
                {
                    // file upload
                    var bytes = new byte[dataLength];
                    Buffer.BlockCopy(Content, dataStart, bytes, 0, dataLength);

                    AddParameter(new ParameterFile(name, filename, ParameterScope.Parameter)
                    {
                        ContentType = contentType,
                        Data = bytes
                    });
                }

                if (isFinal)
                {
                    break;
                }

                pos = nextBoundary;
            }
        }

        /// <summary>
        /// Parses the request content as plain text and extracts parameters from lines in 
        /// the format 'key=value'.
        /// </summary>
        private void ParseTextPlain()
        {
            var text = Encoding.UTF8.GetString(Content);
            var lines = text.Split('\n');

            Parameter last = null;

            foreach (var line in lines)
            {
                var trimmed = line.TrimEnd('\r');

                var match = TextRegex().Match(trimmed);
                if (match.Success)
                {
                    last = new Parameter(match.Groups[1].Value, match.Groups[2].Value, ParameterScope.Parameter);
                    AddParameter(last);
                }
                else
                {
                    last?.Value += "\r\n" + trimmed;
                }
            }

            last?.Value = last.Value.TrimEnd();
        }

        /// <summary>
        /// Parses the request content as a URL-encoded form and adds each key-value pair 
        /// as a parameter.
        /// </summary>
        private void ParseUrlEncoded()
        {
            var text = Encoding.UTF8.GetString(Content);
            foreach (var pair in text.Split('&'))
            {
                // split into at most two parts so that values containing '=' stay intact;
                // '+' decoding is handled by the Parameter constructor's UrlDecode.
                var parts = pair.Split('=', 2);
                var key = parts[0];
                var value = parts.Length > 1 ? parts[1] : string.Empty;

                AddParameter(new Parameter(key, value, ParameterScope.Parameter));
            }
        }

        /// <summary>
        /// Searches for the first occurrence of a specified byte sequence within a byte array, 
        /// starting at a given index.
        /// </summary>
        /// <remarks>
        /// The search is performed using ordinal byte comparison. If needle is an empty array,
        /// the method returns start. If start is greater than haystack.Length - needle.Length, 
        /// the method returns -1.
        /// </remarks>
        /// <param name="haystack">
        /// The byte array to search within.
        /// </param>
        /// <param name="needle">
        /// The byte sequence to locate within the haystack array.
        /// </param>
        /// <param name="start">
        /// The zero-based index in the haystack array at which to begin searching. Must be 
        /// non-negative and less than or equal to haystack.Length.
        /// </param>
        /// <returns>
        /// The zero-based index of the first occurrence of needle within haystack, starting at 
        /// the specified index; or -1 if the sequence is not found.
        /// </returns>
        private static int IndexOf(byte[] haystack, byte[] needle, int start)
        {
            for (int i = start; i <= haystack.Length - needle.Length; i++)
            {
                if (StartsWith(haystack, needle, i))
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>
        /// Finds the end of multipart headers and returns the separator length.
        /// Supports both CRLF and LF line endings.
        /// </summary>
        /// <remarks>
        /// The headers end at the first blank line (two consecutive line breaks). Scanning
        /// forward and stopping there avoids walking the entire body — which may be large and
        /// binary — looking for a separator that only exists right after the part headers.
        /// </remarks>
        private static int FindHeaderEnd(byte[] content, int headerStart, out int separatorLength)
        {
            for (int i = headerStart; i < content.Length; i++)
            {
                var firstBreak = GetLineBreakLength(content, i);
                if (firstBreak == 0)
                {
                    continue;
                }

                var secondBreak = GetLineBreakLength(content, i + firstBreak);
                if (secondBreak > 0)
                {
                    separatorLength = firstBreak + secondBreak;
                    return i;
                }
            }

            separatorLength = 0;
            return -1;
        }

        /// <summary>
        /// Returns the line break length at the specified offset.
        /// Supports CRLF and LF.
        /// </summary>
        private static int GetLineBreakLength(byte[] content, int offset)
        {
            if (offset + 1 < content.Length && content[offset] == (byte)'\r' && content[offset + 1] == (byte)'\n')
            {
                return 2;
            }

            if (offset < content.Length && content[offset] == (byte)'\n')
            {
                return 1;
            }

            return 0;
        }

        /// <summary>
        /// Returns the line break length directly before the specified offset.
        /// Supports CRLF and LF.
        /// </summary>
        private static int GetTrailingLineBreakLength(byte[] content, int offset)
        {
            if (offset >= 2 && content[offset - 2] == (byte)'\r' && content[offset - 1] == (byte)'\n')
            {
                return 2;
            }

            if (offset >= 1 && content[offset - 1] == (byte)'\n')
            {
                return 1;
            }

            return 0;
        }

        /// <summary>
        /// Determines whether a specified segment of a byte array begins with the given prefix.
        /// </summary>
        /// <remarks>
        /// If <paramref name="offset"/> plus the length of <paramref name="prefix"/> exceeds the
        /// length of <paramref name="data"/>, the method returns <see langword="false"/>.
        /// </remarks>
        /// <param name="data">
        /// The byte array to examine.
        /// </param>
        /// <param name="prefix">
        /// The byte sequence to compare against the segment of <paramref name="data"/>.
        /// </param>
        /// <param name="offset">
        /// The zero-based index in <paramref name="data"/> at which to begin the comparison.
        /// </param>
        /// <returns>
        /// <see langword="true"/> if the segment of <paramref name="data"/> starting at 
        /// <paramref name="offset"/> begins with <paramref name="prefix"/>; otherwise, 
        /// <see langword="false"/>.
        /// </returns>
        private static bool StartsWith(byte[] data, byte[] prefix, int offset)
        {
            if (offset + prefix.Length > data.Length)
            {
                return false;
            }

            for (int i = 0; i < prefix.Length; i++)
            {
                if (data[offset + i] != prefix[i])
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Extracts the value of the Content-Type header from the specified header string.
        /// </summary>
        /// <param name="header">
        /// The header string from which to extract the Content-Type value. This should contain a 
        /// line starting with 'Content-Type:'.
        /// </param>
        /// <returns>
        /// A string containing the value of the Content-Type header if found; otherwise, 
        /// an empty string.
        /// </returns>
        private static string ExtractContentType(string header)
        {
            var match = ContentRegex().Match(header);
            return match.Success ? match.Groups[1].Value.Trim() : string.Empty;
        }
    }
}
