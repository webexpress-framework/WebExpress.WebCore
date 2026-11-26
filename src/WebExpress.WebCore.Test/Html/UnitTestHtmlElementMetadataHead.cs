using System.Text;
using System.Text.RegularExpressions;
using WebExpress.WebCore.WebHtml;

namespace WebExpress.WebCore.Test.Html
{
    /// <summary>
    /// Unit tests for the HtmlElementMetadataHead class.
    /// </summary>
    public class UnitTestHtmlElementMetadataHead
    {
        /// <summary>
        /// Tests an empty tag.
        /// </summary>
        [Fact]
        public void Empty()
        {
            // act
            var html = new HtmlElementMetadataHead();

            Assert.Equal(@"<head></head>", html.Trim());
        }

        /// <summary>
        /// Tests that every linked script file is deferred, so the parser is not stopped by any
        /// of them, and that the files keep their order.
        /// </summary>
        [Fact]
        public void ScriptLinksAreDeferred()
        {
            // arrange
            var html = new HtmlElementMetadataHead()
            {
                ScriptLinks = ["/a.js", "/b.js"]
            };

            // act
            var text = html.Trim();

            // validation
            Assert.Contains(@"<script type=""text/javascript"" language=""javascript"" src=""/a.js"" defer></script>", text);
            Assert.Contains(@"<script type=""text/javascript"" language=""javascript"" src=""/b.js"" defer></script>", text);
            Assert.True(text.IndexOf("/a.js") < text.IndexOf("/b.js"), "the files keep their order");
        }

        /// <summary>
        /// Tests that the code of a script that is not a file runs deferred as well, after the
        /// files it builds on: written between the tags, the browser would run it at once,
        /// before the deferred framework it calls.
        /// </summary>
        [Fact]
        public void InlineScriptsRunAfterTheFiles()
        {
            // arrange
            const string code = "webexpress.webui.I18N.register(\"de\", \"x\", { \"a\": \"Größe\" });";
            var html = new HtmlElementMetadataHead()
            {
                ScriptLinks = ["/a.js"],
                Scripts = [code, "let b = 2;"]
            };

            // act
            var text = html.Trim();
            var sources = Regex.Matches(text, @"src=""data:text/javascript;charset=utf-8;base64,([^""]+)"" defer>")
                .Select(x => Encoding.UTF8.GetString(Convert.FromBase64String(x.Groups[1].Value)))
                .ToList();

            // validation
            Assert.Equal([code, "let b = 2;"], sources);
            Assert.DoesNotContain("I18N", text);
            Assert.True(text.IndexOf("/a.js") < text.IndexOf("data:"), "the code follows the files");
        }

        /// <summary>
        /// Tests that the inline scripts read back as they were set, not as the code of the links.
        /// </summary>
        [Fact]
        public void ScriptsReadBackTheInlineScripts()
        {
            // arrange
            var html = new HtmlElementMetadataHead()
            {
                ScriptLinks = ["/a.js"],
                Scripts = ["let a = 1;"]
            };

            // validation
            Assert.Equal(["let a = 1;"], html.Scripts);
        }
    }
}
