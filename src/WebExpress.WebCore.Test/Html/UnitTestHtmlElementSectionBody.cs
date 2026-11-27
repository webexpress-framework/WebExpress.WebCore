using WebExpress.WebCore.WebHtml;

namespace WebExpress.WebCore.Test.Html
{
    /// <summary>
    /// Unit tests for the HtmlElementSectionBody class.
    /// </summary>
    public class UnitTestHtmlElementSectionBody
    {
        /// <summary>
        /// Tests an empty tag.
        /// </summary>
        [Fact]
        public void Empty()
        {
            // act
            var html = new HtmlElementSectionBody();

            Assert.Equal(@"<body></body>", html.Trim());
        }

        /// <summary>
        /// Tests that the scripts at the end of the body are deferred: run at once there, they
        /// would run before the deferred files of the head they build on.
        /// </summary>
        [Fact]
        public void ScriptsAreDeferred()
        {
            // arrange
            var html = new HtmlElementSectionBody()
            {
                ScriptLinks = ["/a.js"],
                Scripts = ["let a = 1;"]
            };

            // act
            var text = html.Trim();

            // validation
            Assert.Contains(@"src=""/a.js"" defer>", text);
            Assert.Contains($@"src=""data:text/javascript;charset=utf-8;base64,{Convert.ToBase64String("let a = 1;"u8.ToArray())}"" defer>", text);
            Assert.DoesNotContain("let a = 1;", text);
        }
    }
}
