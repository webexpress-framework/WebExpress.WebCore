namespace WebExpress.WebCore.WebMessage
{
    /// <summary>
    /// Represents supported content types for file handling and MIME mapping.
    /// </summary>
    public enum ContentType
    {
        /// <summary>
        /// Unknown or unsupported content type.
        /// </summary>
        Unknown,

        /// <summary>
        /// Portable Document Format (.pdf).
        /// </summary>
        Pdf,

        /// <summary>
        /// Plain text file (.txt).
        /// </summary>
        Txt,

        /// <summary>
        /// Cascading Style Sheets (.css).
        /// </summary>
        Css,

        /// <summary>
        /// JavaScript file (.js).
        /// </summary>
        Js,

        /// <summary>
        /// XML file (.xml).
        /// </summary>
        Xml,

        /// <summary>
        /// HTML file (.htm).
        /// </summary>
        Htm,

        /// <summary>
        /// HTML file (.html).
        /// </summary>
        Html,

        /// <summary>
        /// ZIP archive (.zip).
        /// </summary>
        Zip,

        /// <summary>
        /// Microsoft Word document (.doc).
        /// </summary>
        Doc,

        /// <summary>
        /// Microsoft Word Open XML document (.docx).
        /// </summary>
        Docx,

        /// <summary>
        /// Microsoft Excel spreadsheet (.xls).
        /// </summary>
        Xls,

        /// <summary>
        /// Deprecated misspelling of the Excel Open XML spreadsheet. Use <see cref="Xlsx"/> instead;
        /// retained so existing ordinal values and the legacy ".xlx" extension keep resolving.
        /// </summary>
        Xlx,

        /// <summary>
        /// Microsoft PowerPoint presentation (.ppt).
        /// </summary>
        Ppt,

        /// <summary>
        /// Graphics Interchange Format image (.gif).
        /// </summary>
        Gif,

        /// <summary>
        /// Portable Network Graphics image (.png).
        /// </summary>
        Png,

        /// <summary>
        /// Scalable Vector Graphics image (.svg).
        /// </summary>
        Svg,

        /// <summary>
        /// JPEG image (.jpeg).
        /// </summary>
        Jpeg,

        /// <summary>
        /// JPEG image (.jpg).
        /// </summary>
        Jpg,

        /// <summary>
        /// Icon file (.ico).
        /// </summary>
        Ico,

        /// <summary>
        /// WebP image format.
        /// </summary>
        WebP,

        /// <summary>
        /// MPEG audio file (.mp3).
        /// </summary>
        Mp3,

        /// <summary>
        /// MPEG-4 video file (.mp4).
        /// </summary>
        Mp4,

        /// <summary>
        /// JavaScript Object Notation (.json).
        /// </summary>
        Json,

        /// <summary>
        /// Comma-separated values (.csv).
        /// </summary>
        Csv,

        /// <summary>
        /// WebAssembly binary module (.wasm).
        /// </summary>
        Wasm,

        /// <summary>
        /// Web Open Font Format (.woff).
        /// </summary>
        Woff,

        /// <summary>
        /// Web Open Font Format 2 (.woff2).
        /// </summary>
        Woff2,

        /// <summary>
        /// TrueType font (.ttf).
        /// </summary>
        Ttf,

        /// <summary>
        /// OpenType font (.otf).
        /// </summary>
        Otf,

        /// <summary>
        /// Bitmap image (.bmp).
        /// </summary>
        Bmp,

        /// <summary>
        /// AV1 Image File Format (.avif).
        /// </summary>
        Avif,

        /// <summary>
        /// WebM video file (.webm).
        /// </summary>
        Webm,

        /// <summary>
        /// Ogg media container (.ogg).
        /// </summary>
        Ogg,

        /// <summary>
        /// Waveform audio file (.wav).
        /// </summary>
        Wav,

        /// <summary>
        /// Microsoft Excel Open XML spreadsheet (.xlsx).
        /// </summary>
        Xlsx,

        /// <summary>
        /// Microsoft PowerPoint Open XML presentation (.pptx).
        /// </summary>
        Pptx,

        /// <summary>
        /// Gzip-compressed archive (.gz).
        /// </summary>
        Gz,

        /// <summary>
        /// Tape archive (.tar).
        /// </summary>
        Tar,

        /// <summary>
        /// 7-Zip compressed archive (.7z).
        /// </summary>
        SevenZip,

        /// <summary>
        /// Embedded OpenType font (.eot).
        /// </summary>
        Eot,

        /// <summary>
        /// Executable file (.exe). Served as a generic binary download.
        /// </summary>
        Exe
    }

    /// <summary>
    /// Provides extension methods for converting file extensions and MIME types to <see cref="ContentType"/> values.
    /// </summary>
    /// <remarks>
    /// This class includes utility methods for mapping common file formats and media types to their corresponding <c>ContentType</c> enumeration.
    /// It supports both extension-based (e.g. ".png") and MIME-based (e.g. "image/png") conversions.
    /// </remarks>

    public static class ContentTypeExtensions
    {
        /// <summary>
        /// Converts a file extension (e.g. ".png") to a ContentType enum.
        /// </summary>
        public static ContentType ToContentType(string extension)
        {
            if (string.IsNullOrWhiteSpace(extension))
            {
                return ContentType.Unknown;
            }

            extension = extension.Trim().ToLowerInvariant();
            if (!extension.StartsWith("."))
            {
                extension = "." + extension;
            }

            return extension switch
            {
                ".pdf" => ContentType.Pdf,
                ".txt" => ContentType.Txt,
                ".css" => ContentType.Css,
                ".js" => ContentType.Js,
                ".mjs" => ContentType.Js,
                ".map" => ContentType.Json,
                ".xml" => ContentType.Xml,
                ".html" => ContentType.Html,
                ".htm" => ContentType.Htm,
                ".zip" => ContentType.Zip,
                ".doc" => ContentType.Doc,
                ".docx" => ContentType.Docx,
                ".xls" => ContentType.Xls,
                ".xlsx" => ContentType.Xlsx,
                ".xlx" => ContentType.Xlx,
                ".ppt" => ContentType.Ppt,
                ".pptx" => ContentType.Pptx,
                ".gif" => ContentType.Gif,
                ".png" => ContentType.Png,
                ".svg" => ContentType.Svg,
                ".jpeg" => ContentType.Jpeg,
                ".jpg" => ContentType.Jpg,
                ".ico" => ContentType.Ico,
                ".webp" => ContentType.WebP,
                ".bmp" => ContentType.Bmp,
                ".avif" => ContentType.Avif,
                ".mp3" => ContentType.Mp3,
                ".mp4" => ContentType.Mp4,
                ".webm" => ContentType.Webm,
                ".ogg" => ContentType.Ogg,
                ".wav" => ContentType.Wav,
                ".json" => ContentType.Json,
                ".csv" => ContentType.Csv,
                ".wasm" => ContentType.Wasm,
                ".woff" => ContentType.Woff,
                ".woff2" => ContentType.Woff2,
                ".ttf" => ContentType.Ttf,
                ".otf" => ContentType.Otf,
                ".eot" => ContentType.Eot,
                ".gz" => ContentType.Gz,
                ".tar" => ContentType.Tar,
                ".7z" => ContentType.SevenZip,
                ".exe" => ContentType.Exe,
                _ => ContentType.Unknown,
            };
        }

        /// <summary>
        /// Converts a MIME type string (e.g. "application/pdf") to a ContentType enum.
        /// </summary>
        public static ContentType ToContentTypeFromMime(string mimeType)
        {
            if (string.IsNullOrWhiteSpace(mimeType))
            {
                return ContentType.Unknown;
            }

            mimeType = mimeType.Trim().ToLowerInvariant();

            return mimeType switch
            {
                "application/pdf" => ContentType.Pdf,
                "text/plain" => ContentType.Txt,
                "text/css" => ContentType.Css,
                "application/javascript" => ContentType.Js,
                "text/javascript" => ContentType.Js,
                "application/xml" => ContentType.Xml,
                "text/xml" => ContentType.Xml,
                "text/html" => ContentType.Html,
                "application/zip" => ContentType.Zip,
                "application/msword" => ContentType.Doc,
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document" => ContentType.Docx,
                "application/vnd.ms-excel" => ContentType.Xls,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" => ContentType.Xlsx,
                "application/vnd.ms-powerpoint" => ContentType.Ppt,
                "application/vnd.openxmlformats-officedocument.presentationml.presentation" => ContentType.Pptx,
                "image/gif" => ContentType.Gif,
                "image/png" => ContentType.Png,
                "image/svg+xml" => ContentType.Svg,
                "image/jpeg" => ContentType.Jpeg,
                "image/jpg" => ContentType.Jpg,
                "image/x-icon" => ContentType.Ico,
                "image/webp" => ContentType.WebP,
                "image/bmp" => ContentType.Bmp,
                "image/avif" => ContentType.Avif,
                "audio/mpeg" => ContentType.Mp3,
                "video/mp4" => ContentType.Mp4,
                "video/webm" => ContentType.Webm,
                "audio/ogg" => ContentType.Ogg,
                "audio/wav" => ContentType.Wav,
                "application/json" => ContentType.Json,
                "text/csv" => ContentType.Csv,
                "application/wasm" => ContentType.Wasm,
                "font/woff" => ContentType.Woff,
                "font/woff2" => ContentType.Woff2,
                "font/ttf" => ContentType.Ttf,
                "font/otf" => ContentType.Otf,
                "application/vnd.ms-fontobject" => ContentType.Eot,
                "application/gzip" => ContentType.Gz,
                "application/x-tar" => ContentType.Tar,
                "application/x-7z-compressed" => ContentType.SevenZip,
                _ => ContentType.Unknown,
            };
        }

        /// <summary>
        /// Returns the MIME type string associated with the specified <see cref="ContentType"/> value.
        /// </summary>
        /// <param name="extension">The <see cref="ContentType"/> value to convert.</param>
        /// <returns>
        /// A MIME type string such as <c>"image/png"</c> or <c>"application/pdf"</c>.
        /// If the content type is <see cref="ContentType.Unknown"/>, an empty string is returned.
        /// </returns>
        public static string GetMimeType(this ContentType extension)
        {
            return extension switch
            {
                ContentType.Pdf => "application/pdf",
                ContentType.Txt => "text/plain",
                ContentType.Css => "text/css",
                ContentType.Js => "text/javascript",
                ContentType.Xml => "text/xml",
                ContentType.Html => "text/html",
                ContentType.Htm => "text/html",
                ContentType.Zip => "application/zip",
                ContentType.Doc => "application/msword",
                ContentType.Docx => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ContentType.Xls => "application/vnd.ms-excel",
                ContentType.Xlx => "application/vnd.ms-excel",
                ContentType.Xlsx => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                ContentType.Ppt => "application/vnd.ms-powerpoint",
                ContentType.Pptx => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
                ContentType.Gif => "image/gif",
                ContentType.Png => "image/png",
                ContentType.Svg => "image/svg+xml",
                ContentType.Jpeg => "image/jpeg",
                ContentType.Jpg => "image/jpeg",
                ContentType.Ico => "image/x-icon",
                ContentType.WebP => "image/webp",
                ContentType.Bmp => "image/bmp",
                ContentType.Avif => "image/avif",
                ContentType.Mp3 => "audio/mpeg",
                ContentType.Mp4 => "video/mp4",
                ContentType.Webm => "video/webm",
                ContentType.Ogg => "audio/ogg",
                ContentType.Wav => "audio/wav",
                ContentType.Json => "application/json",
                ContentType.Csv => "text/csv",
                ContentType.Wasm => "application/wasm",
                ContentType.Woff => "font/woff",
                ContentType.Woff2 => "font/woff2",
                ContentType.Ttf => "font/ttf",
                ContentType.Otf => "font/otf",
                ContentType.Eot => "application/vnd.ms-fontobject",
                ContentType.Gz => "application/gzip",
                ContentType.Tar => "application/x-tar",
                ContentType.SevenZip => "application/x-7z-compressed",
                ContentType.Exe => "application/octet-stream",
                _ => "application/octet-stream",
            };
        }

        /// <summary>
        /// Returns a file search pattern (e.g. "*.png") associated with the specified <see cref="ContentType"/> value.
        /// </summary>
        /// <param name="extension">The <see cref="ContentType"/> value to convert.</param>
        /// <returns>
        /// A file pattern string such as <c>"*.pdf"</c> or <c>"*.jpg"</c>.
        /// If the content type is <see cref="ContentType.Unknown"/>, an empty string is returned.
        /// </returns>

        public static string GetFilePattern(this ContentType extension)
        {
            return extension switch
            {
                ContentType.Pdf => "*.pdf",
                ContentType.Txt => "*.txt",
                ContentType.Css => "*.css",
                ContentType.Js => "*.js",
                ContentType.Xml => "*.xml",
                ContentType.Html => "*.html",
                ContentType.Htm => "*.htm",
                ContentType.Zip => "*.zip",
                ContentType.Doc => "*.doc",
                ContentType.Docx => "*.docx",
                ContentType.Xls => "*.xls",
                ContentType.Xlx => "*.xlx",
                ContentType.Xlsx => "*.xlsx",
                ContentType.Ppt => "*.ppt",
                ContentType.Pptx => "*.pptx",
                ContentType.Gif => "*.gif",
                ContentType.Png => "*.png",
                ContentType.Svg => "*.svg",
                ContentType.Jpeg => "*.jpeg",
                ContentType.Jpg => "*.jpg",
                ContentType.Ico => "*.ico",
                ContentType.WebP => "*.webp",
                ContentType.Bmp => "*.bmp",
                ContentType.Avif => "*.avif",
                ContentType.Mp3 => "*.mp3",
                ContentType.Mp4 => "*.mp4",
                ContentType.Webm => "*.webm",
                ContentType.Ogg => "*.ogg",
                ContentType.Wav => "*.wav",
                ContentType.Json => "*.json",
                ContentType.Csv => "*.csv",
                ContentType.Wasm => "*.wasm",
                ContentType.Woff => "*.woff",
                ContentType.Woff2 => "*.woff2",
                ContentType.Ttf => "*.ttf",
                ContentType.Otf => "*.otf",
                ContentType.Eot => "*.eot",
                ContentType.Gz => "*.gz",
                ContentType.Tar => "*.tar",
                ContentType.SevenZip => "*.7z",
                ContentType.Exe => "*.exe",
                _ => "*.*",
            };
        }
    }
}
