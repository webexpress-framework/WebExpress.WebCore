using WebExpress.WebCore.WebMessage;

namespace WebExpress.WebCore.Test.Message
{
    /// <summary>
    /// Tests that the name and the type a client declares for an uploaded file cannot point
    /// outside the place an application stores it or carry anything but a media type.
    /// </summary>
    public class UnitTestRequestUpload
    {
        /// <summary>
        /// Verifies that only a plain name remains of a declared file name.
        /// </summary>
        /// <param name="declared">The declared name.</param>
        /// <param name="expected">The name kept.</param>
        [Theory]
        [InlineData("report.pdf", "report.pdf")]
        [InlineData("../../etc/passwd", "passwd")]
        [InlineData(@"..\..\windows\win.ini", "win.ini")]
        [InlineData(@"C:\temp\a.txt", "a.txt")]
        [InlineData("..", "file")]
        [InlineData(".htaccess", "htaccess")]
        [InlineData("a<b>:c\"|?*.txt", "abc.txt")]
        [InlineData("", "file")]
        [InlineData(null, "file")]
        public void FileNameIsPlain(string declared, string expected)
        {
            Assert.Equal(expected, Request.SafeFileName(declared));
        }

        /// <summary>
        /// Verifies that a declared content type is kept only in the form type/subtype.
        /// </summary>
        /// <param name="declared">The declared type.</param>
        /// <param name="expected">The type kept.</param>
        [Theory]
        [InlineData("image/PNG", "image/png")]
        [InlineData("text/plain; charset=utf-8", "text/plain")]
        [InlineData("text/html\r\nX-Injected: 1", "application/octet-stream")]
        [InlineData("<script>", "application/octet-stream")]
        [InlineData(null, "application/octet-stream")]
        public void ContentTypeIsAMediaType(string declared, string expected)
        {
            Assert.Equal(expected, Request.SafeContentType(declared));
        }
    }
}
