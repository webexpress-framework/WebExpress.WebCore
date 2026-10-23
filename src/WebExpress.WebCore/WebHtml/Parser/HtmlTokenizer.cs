using System;
using System.Collections.Generic;
using System.Text;

namespace WebExpress.WebCore.WebHtml.Parser
{
    /// <summary>
    /// Breaks an HTML string into a flat sequence of <see cref="HtmlToken"/> objects.
    /// </summary>
    /// <remarks>
    /// The tokenizer is deliberately lenient: it makes a best-effort attempt to
    /// produce useful tokens even when the input HTML is malformed.  Recoverable
    /// situations (e.g. an unclosed tag or an unquoted attribute value) are handled
    /// silently; only situations that make further tokenization impossible result in a
    /// <see cref="HtmlParseException"/>.
    /// </remarks>
    public class HtmlTokenizer
    {
        private readonly string _input;
        private int _position;
        private string _rawTextTag;

        /// <summary>
        /// Returns the set of HTML void-element tag names that are always
        /// treated as self-closing even when the input does not include a
        /// trailing slash.
        /// </summary>
        private static readonly HashSet<string> VoidElements =
            new(StringComparer.OrdinalIgnoreCase)
            {
                "area", "base", "br", "col", "embed", "hr", "img", "input",
                "keygen", "link", "meta", "param", "source", "track", "wbr"
            };

        /// <summary>
        /// Gets the character at the current position.
        /// </summary>
        private char Current => _position < _input.Length ? _input[_position] : '\0';

        /// <summary>
        /// Initializes a new instance of the <see cref="HtmlTokenizer"/> class.
        /// </summary>
        /// <param name="input">The HTML string to tokenize.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="input"/> is <c>null</c>.</exception>
        public HtmlTokenizer(string input)
        {
            _input = input ?? throw new ArgumentNullException(nameof(input));
        }

        /// <summary>
        /// Tokenizes the entire input and returns all tokens, including a final
        /// <see cref="HtmlTokenType.EndOfFile"/> token.
        /// </summary>
        /// <returns>A list of <see cref="HtmlToken"/> objects.</returns>
        public IReadOnlyList<HtmlToken> Tokenize()
        {
            var tokens = new List<HtmlToken>();
            _position = 0;
            _rawTextTag = null;

            while (_position < _input.Length)
            {
                var token = ReadNextToken();
                if (token is not null)
                {
                    tokens.Add(token);
                }
            }

            tokens.Add(new HtmlToken());
            return tokens;
        }

        /// <summary>
        /// Peeks at a character at the specified offset from the current position.
        /// </summary>
        /// <param name="offset">The offset from the current position.</param>
        /// <returns>
        /// The character at the offset position, or '\0' if out of bounds.
        /// </returns>
        private char Peek(int offset = 1)
        {
            return (_position + offset) < _input.Length
                ? _input[_position + offset]
                : '\0';
        }

        /// <summary>
        /// Reads the next HTML token from the input stream.
        /// </summary>
        /// <returns>
        /// The next HTML token, either a tag or special element if the current
        /// character is '&lt;', otherwise text content.
        /// </returns>
        private HtmlToken ReadNextToken()
        {
            if (_rawTextTag != null)
            {
                return ReadRawText();
            }

            if (Current == '<')
            {
                return ReadTagOrSpecial();
            }

            return ReadText();
        }

        /// <summary>
        /// Reads an HTML token starting with '&lt;' and determines its type 
        /// based on the following character.
        /// </summary>
        /// <returns>
        /// An HtmlToken representing a bang token, end tag, start tag, or text.
        /// </returns>
        private HtmlToken ReadTagOrSpecial()
        {
            var start = _position;
            _position++; // consume '<'

            if (Current == '!')
            {
                return ReadBangToken(start);
            }

            if (Current == '/')
            {
                return ReadEndTag();
            }

            if (char.IsLetter(Current) || Current == '_')
            {
                return ReadStartTag();
            }

            // anything else – treat the stray '<' as text
            return new HtmlToken(HtmlTokenType.Text, "<");
        }

        /// <summary>
        /// Reads an HTML token starting with '!' (bang character), which may 
        /// be a comment, DOCTYPE declaration, or unknown construct.
        /// </summary>
        /// <param name="start">
        /// The starting position of the '!' character in the input.
        /// </param>
        /// <returns>
        /// An HTML token representing a comment, DOCTYPE declaration, or text 
        /// token for unknown constructs.
        /// </returns>
        private HtmlToken ReadBangToken(int start)
        {
            _position++; // consume '!'

            if (Current == '-' && Peek() == '-')
            {
                return ReadComment();
            }

            if (_input.IndexOf("DOCTYPE", _position, StringComparison.OrdinalIgnoreCase) == _position)
            {
                return ReadDoctype();
            }

            // unknown bang construct – consume until '>' and emit as text
            var builder = new StringBuilder("<!");
            while (_position < _input.Length && Current != '>')
            {
                builder.Append(Current);
                _position++;
            }
            if (Current == '>')
            {
                builder.Append('>');
                _position++;
            }
            return new HtmlToken(HtmlTokenType.Text, builder.ToString());
        }

        /// <summary>
        /// Reads an HTML comment from the current position and advances past 
        /// the closing delimiter.
        /// </summary>
        /// <returns>
        /// An HtmlToken of type Comment containing the trimmed comment text.
        /// </returns>
        private HtmlToken ReadComment()
        {
            _position += 2; // consume '--'
            var builder = new StringBuilder();

            while (_position < _input.Length)
            {
                if (Current == '-' && Peek() == '-' && Peek(2) == '>')
                {
                    _position += 3; // consume '-->'
                    break;
                }
                builder.Append(Current);
                _position++;
            }

            return new HtmlToken(HtmlTokenType.Comment, builder.ToString().Trim());
        }

        /// <summary>
        /// Reads and parses a DOCTYPE declaration from the input.
        /// </summary>
        /// <returns>
        /// An HTML token representing the DOCTYPE declaration with its name.
        /// </returns>
        private HtmlToken ReadDoctype()
        {
            _position += 7; // consume 'DOCTYPE'
            SkipWhitespace();

            var nameBuilder = new StringBuilder();
            while (_position < _input.Length && Current != '>' && !char.IsWhiteSpace(Current))
            {
                nameBuilder.Append(Current);
                _position++;
            }

            // skip anything remaining until '>'
            while (_position < _input.Length && Current != '>')
            {
                _position++;
            }
            if (Current == '>')
            {
                _position++;
            }

            return new HtmlToken(HtmlTokenType.Doctype, nameBuilder.ToString(), []);
        }

        /// <summary>
        /// Reads an HTML end tag from the current position.
        /// </summary>
        /// <returns>
        /// An HtmlToken representing the parsed end tag, with the tag name 
        /// converted to lowercase.
        /// </returns>
        private HtmlToken ReadEndTag()
        {
            _position++; // consume '/'
            SkipWhitespace();

            var tagName = ReadTagName();
            SkipWhitespace();

            // consume closing '>'
            if (Current == '>')
            {
                _position++;
            }

            return new HtmlToken(HtmlTokenType.EndTag, tagName.ToLowerInvariant(), []);
        }

        /// <summary>
        /// Reads an HTML start tag from the current position, including its 
        /// name and attributes.
        /// </summary>
        /// <returns>
        /// An <see cref="HtmlToken"/> representing either a start tag or 
        /// self-closing tag, depending on the tag syntax and whether it 
        /// is a void element.
        /// </returns>
        private HtmlToken ReadStartTag()
        {
            var tagName = ReadTagName().ToLowerInvariant();
            var attributes = ReadAttributes();

            var selfClose = false;
            if (Current == '/')
            {
                selfClose = true;
                _position++; // consume '/'
            }

            if (Current == '>')
            {
                _position++; // consume '>'
            }

            if (selfClose || VoidElements.Contains(tagName))
            {
                return new HtmlToken(HtmlTokenType.SelfClosingTag, tagName, attributes);
            }

            if (tagName is "script" or "style" or "title" or "textarea" or
                "xmp" or "iframe" or "noembed" or "noframes" or "plaintext")
            {
                _rawTextTag = tagName;
            }

            return new HtmlToken(HtmlTokenType.StartTag, tagName, attributes);
        }

        /// <summary>
        /// Keeps markup-like content inside raw text and escapable raw text elements from becoming tags.
        /// </summary>
        /// <returns>The literal content, or the matching end tag when the content is empty.</returns>
        private HtmlToken ReadRawText()
        {
            var start = _position;
            var closingTag = "</" + _rawTextTag;
            var end = _rawTextTag == "plaintext" ? -1 :
                _input.IndexOf(closingTag, start, StringComparison.OrdinalIgnoreCase);

            while (end >= 0)
            {
                var boundary = end + closingTag.Length;
                if (boundary < _input.Length &&
                    (_input[boundary] is '>' or '/' or ' ' or '\t' or '\r' or '\n' or '\f'))
                {
                    break;
                }

                end = _input.IndexOf(closingTag, boundary, StringComparison.OrdinalIgnoreCase);
            }

            _position = end < 0 ? _input.Length : end;
            _rawTextTag = null;
            return _position > start
                ? new HtmlToken(HtmlTokenType.Text, _input[start.._position])
                : ReadTagOrSpecial();
        }

        /// <summary>
        /// Reads the tag name at the current position.
        /// </summary>
        /// <returns>
        /// The tag name that was read.
        /// </returns>
        private string ReadTagName()
        {
            var builder = new StringBuilder();
            while (_position < _input.Length && !char.IsWhiteSpace(Current) &&
                   Current != '>' && Current != '/' && Current != '\0')
            {
                builder.Append(Current);
                _position++;
            }
            return builder.ToString();
        }

        /// <summary>
        /// Reads all HTML attributes from the current position up to a 
        /// terminating character.
        /// </summary>
        /// <returns>
        /// A read-only list of the parsed HTML attributes.
        /// </returns>
        private IReadOnlyList<HtmlTokenAttribute> ReadAttributes()
        {
            var list = new List<HtmlTokenAttribute>();

            while (_position < _input.Length)
            {
                SkipWhitespace();

                if (Current == '>' || Current == '/' || Current == '\0')
                {
                    break;
                }

                var attr = ReadAttribute();
                if (attr != null)
                {
                    list.Add(attr);
                }
            }

            return list;
        }

        /// <summary>
        /// Reads an HTML attribute (name and optional value) from the 
        /// current position.
        /// </summary>
        /// <returns>
        /// An HtmlTokenAttribute containing the attribute name and optional 
        /// value, or null if no valid attribute could be read.
        /// </returns>
        private HtmlTokenAttribute ReadAttribute()
        {
            var name = ReadAttributeName();
            if (string.IsNullOrEmpty(name))
            {
                // skip an unexpected character and continue
                if (_position < _input.Length)
                {
                    _position++;
                }
                return null;
            }

            SkipWhitespace();

            if (Current != '=')
            {
                // boolean attribute
                return new HtmlTokenAttribute(name.ToLowerInvariant());
            }

            _position++; // consume '='
            SkipWhitespace();

            var value = ReadAttributeValue();
            return new HtmlTokenAttribute(name.ToLowerInvariant(), value);
        }

        /// <summary>
        /// Reads an attribute name from the current position in the input 
        /// until a delimiter is reached (=, >, /, whitespace, or a null character).
        /// </summary>
        /// <returns>
        /// The attribute name that was read as a string.
        /// </returns>
        private string ReadAttributeName()
        {
            var builder = new StringBuilder();
            while (_position < _input.Length && Current != '=' && Current != '>' &&
                   Current != '/' && !char.IsWhiteSpace(Current) && Current != '\0')
            {
                builder.Append(Current);
                _position++;
            }
            return builder.ToString();
        }

        /// <summary>
        /// Reads an attribute value from the input, either quoted or unquoted.
        /// </summary>
        /// <remarks>
        /// Supports both single and double quotation marks for quoted values.
        /// Unquoted values are read until the next whitespace or '>' character.
        /// </remarks>
        /// <returns>
        /// The attribute value that was read.
        /// </returns>
        private string ReadAttributeValue()
        {
            if (Current == '"' || Current == '\'')
            {
                var quote = Current;
                _position++; // consume opening quote
                var builder = new StringBuilder();
                while (_position < _input.Length && Current != quote)
                {
                    builder.Append(Current);
                    _position++;
                }
                if (Current == quote)
                {
                    _position++; // consume closing quote
                }
                return builder.ToString();
            }

            // unquoted value – read until whitespace or '>'
            var unquotedBuilder = new StringBuilder();
            while (_position < _input.Length && !char.IsWhiteSpace(Current) &&
                   Current != '>' && Current != '\0')
            {
                unquotedBuilder.Append(Current);
                _position++;
            }
            return unquotedBuilder.ToString();
        }

        /// <summary>
        /// Reads text content from the current position until a '&lt;' character 
        /// is encountered or the end of input is reached.
        /// </summary>
        /// <returns>
        /// An <see cref="HtmlToken"/> with type Text containing the read content, 
        /// or <see langword="null"/> if no text was read.
        /// </returns>
        private HtmlToken ReadText()
        {
            var builder = new StringBuilder();
            while (_position < _input.Length && Current != '<')
            {
                builder.Append(Current);
                _position++;
            }

            var text = builder.ToString();
            return text.Length > 0 ? new HtmlToken(HtmlTokenType.Text, text) : null;
        }

        /// <summary>
        /// Skips whitespace starting at the current position.
        /// </summary>
        private void SkipWhitespace()
        {
            while (_position < _input.Length && char.IsWhiteSpace(Current))
            {
                _position++;
            }
        }
    }
}
