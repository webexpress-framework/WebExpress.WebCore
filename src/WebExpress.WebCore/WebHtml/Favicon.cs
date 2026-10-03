namespace WebExpress.WebCore.WebHtml
{
    /// <summary>
    /// Represents a favicon with a URL and media type.
    /// </summary>
    public class Favicon
    {
        /// <summary>
        /// Gets or sets the uri.
        /// </summary>
        public string Url { get; set; }

        /// <summary>
        /// Gets or sets the media type.
        /// </summary>
        public TypeFavicon Mediatype { get; set; }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="url">The uri.</param>
        /// <param name="mediatype">The media type.</param>
        public Favicon(string url, TypeFavicon mediatype)
        {
            Url = url;
            Mediatype = mediatype;
        }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="url">The uri.</param>
        public Favicon(string url)
        {
            Url = url;
        }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="url">The uri.</param>
        /// <param name="mediatype">The media type.</param>
        public Favicon(string url, string mediatype)
        {
            Url = url;

            switch (mediatype)
            {
                case "image/x-icon":
                    Mediatype = TypeFavicon.ICON;
                    break;
                case "image/jpg":
                    Mediatype = TypeFavicon.JPG;
                    break;
                case "image/png":
                    Mediatype = TypeFavicon.PNG;
                    break;
                case "image/svg+xml":
                    Mediatype = TypeFavicon.SVG;
                    break;
                default:
                    break;
            }
        }

        /// <summary>
        /// Returns the media type.
        /// </summary>
        /// <returns>The media type.</returns>
        public string GetMediaType()
        {
            return Mediatype switch
            {
                TypeFavicon.ICON => "image/x-icon",
                TypeFavicon.JPG => "image/jpg",
                TypeFavicon.PNG => "image/png",
                TypeFavicon.SVG => "image/svg+xml",
                _ => "",
            };
        }
    }
}
