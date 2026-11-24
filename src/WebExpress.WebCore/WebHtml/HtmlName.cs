namespace WebExpress.WebCore.WebHtml
{
    /// <summary>
    /// Checks names of elements and attributes before they are written into markup.
    /// </summary>
    /// <remarks>
    /// A name is written as it is, since it cannot be escaped. Names reach the writer from parsed
    /// user HTML and from application code, so a name that could end the tag or start another
    /// attribute is refused instead of being written.
    /// </remarks>
    public static class HtmlName
    {
        /// <summary>
        /// Tells whether a name can be written as the name of an element: a letter followed by
        /// letters, digits, hyphens, underscores, colons or periods.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <returns>True if the name is safe to write.</returns>
        public static bool IsValidElementName(string name)
        {
            if (string.IsNullOrEmpty(name) || !char.IsAsciiLetter(name[0]))
            {
                return false;
            }

            foreach (var c in name)
            {
                if (!char.IsAsciiLetterOrDigit(c) && c is not ('-' or '_' or ':' or '.'))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Tells whether a name can be written as the name of an attribute. It must not be empty
        /// and must not hold whitespace, quotes, the characters that end a tag or an attribute
        /// name, or control characters.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <returns>True if the name is safe to write.</returns>
        public static bool IsValidAttributeName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return false;
            }

            foreach (var c in name)
            {
                if (char.IsControl(c) || char.IsWhiteSpace(c)
                    || c is '"' or '\'' or '>' or '<' or '/' or '=' or '`')
                {
                    return false;
                }
            }

            return true;
        }
    }
}
