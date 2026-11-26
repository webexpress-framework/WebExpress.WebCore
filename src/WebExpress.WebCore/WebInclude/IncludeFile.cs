namespace WebExpress.WebCore.WebInclude
{
    /// <summary>
    /// Represents a file to be included, along with its associated type information.
    /// </summary>
    /// <remarks>
    /// This class is used to specify a file and its corresponding type for inclusion in a process or
    /// operation.
    /// </remarks>
    public class IncludeFile
    {
        /// <summary>
        /// Gets or sets the type to be included in the operation.
        /// </summary>
        public TypeInclude Type { get; set; }

        /// <summary>
        /// Gets or sets the name of the file, including its extension.
        /// </summary>
        public string FileName { get; set; }

        /// <summary>
        /// Gets or sets the route the file is linked from within the application of its include.
        /// </summary>
        /// <remarks>
        /// The include manager resolves it once at registration, carrying a hash of the content
        /// for scripts and stylesheets, so a page links it without composing the route again.
        /// It is null for a file the include manager did not resolve.
        /// </remarks>
        public string Route { get; set; }
    }
}
