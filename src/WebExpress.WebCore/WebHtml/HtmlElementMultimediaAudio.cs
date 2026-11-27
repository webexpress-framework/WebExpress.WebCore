namespace WebExpress.WebCore.WebHtml
{
    /// <summary>
    /// Represents a sound file or audio stream.
    /// </summary>
    public class HtmlElementMultimediaAudio : HtmlElement, IHtmlElementMultimedia
    {
        /// <summary>
        /// Gets or sets the audio URL.
        /// </summary>
        public string Src
        {
            get => GetAttribute("src");
            set => SetAttribute("src", value);
        }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public HtmlElementMultimediaAudio()
            : base("audio")
        {
        }
    }
}
