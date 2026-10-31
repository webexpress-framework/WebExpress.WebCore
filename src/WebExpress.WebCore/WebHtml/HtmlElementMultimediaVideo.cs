namespace WebExpress.WebCore.WebHtml
{
    /// <summary>
    /// Represents a video file and its audio files, as well as the controls needed to play it.
    /// </summary>
    public class HtmlElementMultimediaVideo : HtmlElement, IHtmlElementMultimedia
    {
        /// <summary>
        /// Gets or sets the video uri.
        /// </summary>
        public string Src
        {
            get => GetAttribute("src");
            set => SetAttribute("src", value);
        }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public HtmlElementMultimediaVideo()
            : base("video")
        {

        }
    }
}
