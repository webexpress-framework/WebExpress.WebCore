using WebExpress.WebCore.WebHtml;

namespace WebExpress.WebCore.Test.Html
{
    /// <summary>
    /// Tests that user HTML is reduced to markup that cannot run script.
    /// </summary>
    public class UnitTestHtmlSanitizer
    {
        /// <summary>
        /// Verifies that script vectors are removed and harmless markup is kept.
        /// </summary>
        /// <param name="html">The user HTML.</param>
        /// <param name="expected">The sanitized markup.</param>
        [Theory]
        [InlineData("<b>bold</b>", "<b>bold</b>")]
        [InlineData("<script>alert(1)</script>x", "x")]
        [InlineData("<img src=x onerror=alert(1)>", "<img src=\"x\">")]
        [InlineData("<a href=\"javascript:alert(1)\">x</a>", "<a>x</a>")]
        [InlineData("<a href=\"java&#9;script:alert(1)\">x</a>", "<a>x</a>")]
        [InlineData("<a href=\"/page\">x</a>", "<a href=\"/page\" rel=\"nofollow noopener noreferrer\">x</a>")]
        [InlineData("<span style=\"color: red; position: fixed\">r</span>", "<span style=\"color: red;\">r</span>")]
        [InlineData("<div data-wx-primary-uri=\"/x\" id=\"y\">d</div>", "<div>d</div>")]
        [InlineData("<!-- a --!><svg onload=alert(1)>", "")]
        [InlineData("<unknown>text</unknown>", "text")]
        [InlineData("a < b & c", "a &lt; b &amp; c")]
        public void Sanitize(string html, string expected)
        {
            Assert.Equal(expected, HtmlSanitizer.Sanitize(html));
        }

        /// <summary>
        /// Verifies the link and image address policy.
        /// </summary>
        [Fact]
        public void UrlPolicyAllowsOnlyKnownSchemes()
        {
            Assert.True(UrlPolicy.IsSafeLink("https://example.com"));
            Assert.True(UrlPolicy.IsSafeLink("relative/path"));
            Assert.False(UrlPolicy.IsSafeLink(" JavaScript:alert(1)"));
            Assert.False(UrlPolicy.IsSafeLink("data:text/html,<script>"));
            Assert.True(UrlPolicy.IsSafeImage("data:image/png;base64,AA=="));
            Assert.False(UrlPolicy.IsSafeImage("data:image/svg+xml,<svg onload=alert(1)>"));
        }
    }
}
