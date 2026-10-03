using WebExpress.WebCore.WebMessage;

namespace WebExpress.WebCore.Test.Message
{
    /// <summary>
    /// Unit tests for the ContentType enum and the ContentTypeExtensions mapping helpers.
    /// </summary>
    public class UnitTestContentType
    {
        /// <summary>
        /// Tests that known file extensions are mapped to the matching content type.
        /// </summary>
        [Theory]
        [InlineData(".pdf", ContentType.Pdf)]
        [InlineData(".txt", ContentType.Txt)]
        [InlineData(".css", ContentType.Css)]
        [InlineData(".js", ContentType.Js)]
        [InlineData(".mjs", ContentType.Js)]
        [InlineData(".map", ContentType.Json)]
        [InlineData(".eot", ContentType.Eot)]
        [InlineData(".xml", ContentType.Xml)]
        [InlineData(".html", ContentType.Html)]
        [InlineData(".htm", ContentType.Htm)]
        [InlineData(".zip", ContentType.Zip)]
        [InlineData(".doc", ContentType.Doc)]
        [InlineData(".docx", ContentType.Docx)]
        [InlineData(".xls", ContentType.Xls)]
        [InlineData(".xlsx", ContentType.Xlsx)]
        [InlineData(".xlx", ContentType.Xlx)]
        [InlineData(".ppt", ContentType.Ppt)]
        [InlineData(".pptx", ContentType.Pptx)]
        [InlineData(".gif", ContentType.Gif)]
        [InlineData(".png", ContentType.Png)]
        [InlineData(".svg", ContentType.Svg)]
        [InlineData(".jpeg", ContentType.Jpeg)]
        [InlineData(".jpg", ContentType.Jpg)]
        [InlineData(".ico", ContentType.Ico)]
        [InlineData(".webp", ContentType.WebP)]
        [InlineData(".bmp", ContentType.Bmp)]
        [InlineData(".avif", ContentType.Avif)]
        [InlineData(".mp3", ContentType.Mp3)]
        [InlineData(".mp4", ContentType.Mp4)]
        [InlineData(".webm", ContentType.Webm)]
        [InlineData(".ogg", ContentType.Ogg)]
        [InlineData(".wav", ContentType.Wav)]
        [InlineData(".json", ContentType.Json)]
        [InlineData(".csv", ContentType.Csv)]
        [InlineData(".wasm", ContentType.Wasm)]
        [InlineData(".woff", ContentType.Woff)]
        [InlineData(".woff2", ContentType.Woff2)]
        [InlineData(".ttf", ContentType.Ttf)]
        [InlineData(".otf", ContentType.Otf)]
        [InlineData(".gz", ContentType.Gz)]
        [InlineData(".tar", ContentType.Tar)]
        [InlineData(".7z", ContentType.SevenZip)]
        public void ToContentTypeMapsExtension(string extension, ContentType expected)
        {
            Assert.Equal(expected, ContentTypeExtensions.ToContentType(extension));
        }

        /// <summary>
        /// Tests that the extension input is normalized before mapping, so a missing
        /// leading dot, surrounding whitespace and casing do not affect the result.
        /// </summary>
        [Theory]
        [InlineData("png")]
        [InlineData(".PNG")]
        [InlineData("PNG")]
        [InlineData("  .png  ")]
        [InlineData(" .PnG ")]
        public void ToContentTypeNormalizesInput(string extension)
        {
            Assert.Equal(ContentType.Png, ContentTypeExtensions.ToContentType(extension));
        }

        /// <summary>
        /// Tests that empty or unmapped extensions resolve to the unknown content type.
        /// </summary>
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(".foo")]
        [InlineData(".")]
        public void ToContentTypeReturnsUnknown(string extension)
        {
            Assert.Equal(ContentType.Unknown, ContentTypeExtensions.ToContentType(extension));
        }

        /// <summary>
        /// Tests that known MIME types are mapped to the matching content type.
        /// </summary>
        [Theory]
        [InlineData("application/pdf", ContentType.Pdf)]
        [InlineData("text/plain", ContentType.Txt)]
        [InlineData("text/css", ContentType.Css)]
        [InlineData("application/javascript", ContentType.Js)]
        [InlineData("text/javascript", ContentType.Js)]
        [InlineData("application/xml", ContentType.Xml)]
        [InlineData("text/xml", ContentType.Xml)]
        [InlineData("text/html", ContentType.Html)]
        [InlineData("application/zip", ContentType.Zip)]
        [InlineData("application/msword", ContentType.Doc)]
        [InlineData("application/vnd.openxmlformats-officedocument.wordprocessingml.document", ContentType.Docx)]
        [InlineData("application/vnd.ms-excel", ContentType.Xls)]
        [InlineData("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", ContentType.Xlsx)]
        [InlineData("application/vnd.ms-powerpoint", ContentType.Ppt)]
        [InlineData("application/vnd.openxmlformats-officedocument.presentationml.presentation", ContentType.Pptx)]
        [InlineData("image/gif", ContentType.Gif)]
        [InlineData("image/png", ContentType.Png)]
        [InlineData("image/svg+xml", ContentType.Svg)]
        [InlineData("image/jpeg", ContentType.Jpeg)]
        [InlineData("image/jpg", ContentType.Jpg)]
        [InlineData("image/x-icon", ContentType.Ico)]
        [InlineData("image/webp", ContentType.WebP)]
        [InlineData("image/bmp", ContentType.Bmp)]
        [InlineData("image/avif", ContentType.Avif)]
        [InlineData("audio/mpeg", ContentType.Mp3)]
        [InlineData("video/mp4", ContentType.Mp4)]
        [InlineData("video/webm", ContentType.Webm)]
        [InlineData("audio/ogg", ContentType.Ogg)]
        [InlineData("audio/wav", ContentType.Wav)]
        [InlineData("application/json", ContentType.Json)]
        [InlineData("text/csv", ContentType.Csv)]
        [InlineData("application/wasm", ContentType.Wasm)]
        [InlineData("font/woff", ContentType.Woff)]
        [InlineData("font/woff2", ContentType.Woff2)]
        [InlineData("font/ttf", ContentType.Ttf)]
        [InlineData("font/otf", ContentType.Otf)]
        [InlineData("application/vnd.ms-fontobject", ContentType.Eot)]
        [InlineData("application/gzip", ContentType.Gz)]
        [InlineData("application/x-tar", ContentType.Tar)]
        [InlineData("application/x-7z-compressed", ContentType.SevenZip)]
        public void ToContentTypeFromMimeMapsMime(string mimeType, ContentType expected)
        {
            Assert.Equal(expected, ContentTypeExtensions.ToContentTypeFromMime(mimeType));
        }

        /// <summary>
        /// Tests that the MIME input is normalized for casing and surrounding whitespace before mapping.
        /// </summary>
        [Theory]
        [InlineData("TEXT/HTML")]
        [InlineData("  text/html  ")]
        [InlineData("Text/Html")]
        public void ToContentTypeFromMimeNormalizesInput(string mimeType)
        {
            Assert.Equal(ContentType.Html, ContentTypeExtensions.ToContentTypeFromMime(mimeType));
        }

        /// <summary>
        /// Tests that empty or unmapped MIME types resolve to the unknown content type.
        /// </summary>
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("application/foo")]
        public void ToContentTypeFromMimeReturnsUnknown(string mimeType)
        {
            Assert.Equal(ContentType.Unknown, ContentTypeExtensions.ToContentTypeFromMime(mimeType));
        }

        /// <summary>
        /// Tests that each content type reports its MIME type. The mapping is intentionally
        /// lossy: htm/html share text/html, and the legacy xlx still maps to the binary
        /// ms-excel type, whereas xlsx now uses the dedicated Open XML media type.
        /// </summary>
        [Theory]
        [InlineData(ContentType.Pdf, "application/pdf")]
        [InlineData(ContentType.Txt, "text/plain")]
        [InlineData(ContentType.Css, "text/css")]
        [InlineData(ContentType.Js, "text/javascript")]
        [InlineData(ContentType.Xml, "text/xml")]
        [InlineData(ContentType.Html, "text/html")]
        [InlineData(ContentType.Htm, "text/html")]
        [InlineData(ContentType.Zip, "application/zip")]
        [InlineData(ContentType.Doc, "application/msword")]
        [InlineData(ContentType.Docx, "application/vnd.openxmlformats-officedocument.wordprocessingml.document")]
        [InlineData(ContentType.Xls, "application/vnd.ms-excel")]
        [InlineData(ContentType.Xlx, "application/vnd.ms-excel")]
        [InlineData(ContentType.Xlsx, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
        [InlineData(ContentType.Ppt, "application/vnd.ms-powerpoint")]
        [InlineData(ContentType.Pptx, "application/vnd.openxmlformats-officedocument.presentationml.presentation")]
        [InlineData(ContentType.Gif, "image/gif")]
        [InlineData(ContentType.Png, "image/png")]
        [InlineData(ContentType.Svg, "image/svg+xml")]
        [InlineData(ContentType.Jpeg, "image/jpeg")]
        [InlineData(ContentType.Jpg, "image/jpeg")]
        [InlineData(ContentType.Ico, "image/x-icon")]
        [InlineData(ContentType.WebP, "image/webp")]
        [InlineData(ContentType.Bmp, "image/bmp")]
        [InlineData(ContentType.Avif, "image/avif")]
        [InlineData(ContentType.Mp3, "audio/mpeg")]
        [InlineData(ContentType.Mp4, "video/mp4")]
        [InlineData(ContentType.Webm, "video/webm")]
        [InlineData(ContentType.Ogg, "audio/ogg")]
        [InlineData(ContentType.Wav, "audio/wav")]
        [InlineData(ContentType.Json, "application/json")]
        [InlineData(ContentType.Csv, "text/csv")]
        [InlineData(ContentType.Wasm, "application/wasm")]
        [InlineData(ContentType.Woff, "font/woff")]
        [InlineData(ContentType.Woff2, "font/woff2")]
        [InlineData(ContentType.Ttf, "font/ttf")]
        [InlineData(ContentType.Otf, "font/otf")]
        [InlineData(ContentType.Eot, "application/vnd.ms-fontobject")]
        [InlineData(ContentType.Gz, "application/gzip")]
        [InlineData(ContentType.Tar, "application/x-tar")]
        [InlineData(ContentType.SevenZip, "application/x-7z-compressed")]
        public void GetMimeTypeReturnsMime(ContentType contentType, string expected)
        {
            Assert.Equal(expected, contentType.GetMimeType());
        }

        /// <summary>
        /// Tests that the unknown content type falls back to the generic binary MIME type.
        /// </summary>
        [Fact]
        public void GetMimeTypeUnknownReturnsOctetStream()
        {
            Assert.Equal("application/octet-stream", ContentType.Unknown.GetMimeType());
        }

        /// <summary>
        /// Tests that the Open XML office MIME types round-trip back to their dedicated content
        /// types, guarding against the previous behavior where docx/xlsx collapsed onto the
        /// legacy binary office formats.
        /// </summary>
        [Theory]
        [InlineData(ContentType.Docx)]
        [InlineData(ContentType.Xlsx)]
        [InlineData(ContentType.Pptx)]
        public void OpenXmlOfficeTypesRoundTrip(ContentType contentType)
        {
            Assert.Equal(contentType, ContentTypeExtensions.ToContentTypeFromMime(contentType.GetMimeType()));
        }

        /// <summary>
        /// Tests that each content type reports a file search pattern matching its extension.
        /// </summary>
        [Theory]
        [InlineData(ContentType.Pdf, "*.pdf")]
        [InlineData(ContentType.Txt, "*.txt")]
        [InlineData(ContentType.Css, "*.css")]
        [InlineData(ContentType.Js, "*.js")]
        [InlineData(ContentType.Xml, "*.xml")]
        [InlineData(ContentType.Html, "*.html")]
        [InlineData(ContentType.Htm, "*.htm")]
        [InlineData(ContentType.Zip, "*.zip")]
        [InlineData(ContentType.Doc, "*.doc")]
        [InlineData(ContentType.Docx, "*.docx")]
        [InlineData(ContentType.Xls, "*.xls")]
        [InlineData(ContentType.Xlx, "*.xlx")]
        [InlineData(ContentType.Xlsx, "*.xlsx")]
        [InlineData(ContentType.Ppt, "*.ppt")]
        [InlineData(ContentType.Pptx, "*.pptx")]
        [InlineData(ContentType.Gif, "*.gif")]
        [InlineData(ContentType.Png, "*.png")]
        [InlineData(ContentType.Svg, "*.svg")]
        [InlineData(ContentType.Jpeg, "*.jpeg")]
        [InlineData(ContentType.Jpg, "*.jpg")]
        [InlineData(ContentType.Ico, "*.ico")]
        [InlineData(ContentType.WebP, "*.webp")]
        [InlineData(ContentType.Bmp, "*.bmp")]
        [InlineData(ContentType.Avif, "*.avif")]
        [InlineData(ContentType.Mp3, "*.mp3")]
        [InlineData(ContentType.Mp4, "*.mp4")]
        [InlineData(ContentType.Webm, "*.webm")]
        [InlineData(ContentType.Ogg, "*.ogg")]
        [InlineData(ContentType.Wav, "*.wav")]
        [InlineData(ContentType.Json, "*.json")]
        [InlineData(ContentType.Csv, "*.csv")]
        [InlineData(ContentType.Wasm, "*.wasm")]
        [InlineData(ContentType.Woff, "*.woff")]
        [InlineData(ContentType.Woff2, "*.woff2")]
        [InlineData(ContentType.Ttf, "*.ttf")]
        [InlineData(ContentType.Otf, "*.otf")]
        [InlineData(ContentType.Eot, "*.eot")]
        [InlineData(ContentType.Gz, "*.gz")]
        [InlineData(ContentType.Tar, "*.tar")]
        [InlineData(ContentType.SevenZip, "*.7z")]
        public void GetFilePatternReturnsPattern(ContentType contentType, string expected)
        {
            Assert.Equal(expected, contentType.GetFilePattern());
        }

        /// <summary>
        /// Tests that the unknown content type falls back to the match-all file pattern.
        /// </summary>
        [Fact]
        public void GetFilePatternUnknownReturnsWildcard()
        {
            Assert.Equal("*.*", ContentType.Unknown.GetFilePattern());
        }
    }
}
