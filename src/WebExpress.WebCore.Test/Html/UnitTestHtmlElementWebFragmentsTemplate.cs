using WebExpress.WebCore.WebHtml;

namespace WebExpress.WebCore.Test.Html
{
    /// <summary>
    /// Unit tests for the HtmlElementWebFragmentsTemplate class.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestHtmlElementWebFragmentsTemplate
    {
        /// <summary>
        /// Tests an empty tag.
        /// </summary>
        [Fact]
        public void Empty()
        {
            // act
            var html = new HtmlElementWebFragmentsTemplate();

            Assert.Equal(@"<template></template>", html.Trim());
        }
    }
}
