using WebExpress.WebCore.WebHtml;
using WebExpress.WebCore.WebHtml.Parser;

namespace WebExpress.WebCore.Test.Html.Parser
{
    /// <summary>
    /// Unit tests for the <see cref="HtmlElementFactory"/> class.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestHtmlElementFactory
    {
        /// <summary>
        /// Known tags are resolved to their specific subclass.
        /// </summary>
        [Fact]
        public void KnownTag_Div_ReturnsCorrectType()
        {
            var element = HtmlElementFactory.Create("div");

            Assert.IsType<HtmlElementTextContentDiv>(element);
        }

        /// <summary>
        /// Known tags are resolved to their specific subclass.
        /// </summary>
        [Fact]
        public void KnownTag_Span_ReturnsCorrectType()
        {
            var element = HtmlElementFactory.Create("span");

            Assert.IsType<HtmlElementTextSemanticsSpan>(element);
        }

        /// <summary>
        /// Known tags are resolved to their specific subclass.
        /// </summary>
        [Fact]
        public void KnownTag_Img_ReturnsCorrectType()
        {
            var element = HtmlElementFactory.Create("img");

            Assert.IsType<HtmlElementMultimediaImg>(element);
        }

        /// <summary>
        /// Known tags are resolved to their specific subclass.
        /// </summary>
        [Fact]
        public void KnownTag_Input_ReturnsCorrectType()
        {
            var element = HtmlElementFactory.Create("input");

            Assert.IsType<HtmlElementFieldInput>(element);
        }

        /// <summary>
        /// Known tags are resolved to their specific subclass.
        /// </summary>
        [Fact]
        public void KnownTag_Table_ReturnsCorrectType()
        {
            var element = HtmlElementFactory.Create("table");

            Assert.IsType<HtmlElementTableTable>(element);
        }

        /// <summary>
        /// Factory is case-insensitive – upper-case tag names resolve correctly.
        /// </summary>
        [Fact]
        public void CaseInsensitive_UpperCase_ReturnsCorrectType()
        {
            var element = HtmlElementFactory.Create("DIV");

            Assert.IsType<HtmlElementTextContentDiv>(element);
        }

        /// <summary>
        /// An unknown tag name returns a generic <see cref="HtmlElement"/>.
        /// </summary>
        [Fact]
        public void UnknownTag_ReturnsGenericElement()
        {
            var element = HtmlElementFactory.Create("x-custom-widget");

            Assert.IsType<HtmlElement>(element);
        }

        /// <summary>
        /// <see cref="HtmlElementFactory.IsKnown"/> returns <c>true</c> for known tags.
        /// </summary>
        [Fact]
        public void IsKnown_KnownTag_ReturnsTrue()
        {
            Assert.True(HtmlElementFactory.IsKnown("div"));
        }

        /// <summary>
        /// <see cref="HtmlElementFactory.IsKnown"/> returns <c>false</c> for unknown tags.
        /// </summary>
        [Fact]
        public void IsKnown_UnknownTag_ReturnsFalse()
        {
            Assert.False(HtmlElementFactory.IsKnown("x-unknown"));
        }

        /// <summary>
        /// Passing <c>null</c> to <see cref="HtmlElementFactory.Create"/> throws an
        /// <see cref="System.ArgumentNullException"/>.
        /// </summary>
        [Fact]
        public void NullTagName_Throws()
        {
            Assert.Throws<System.ArgumentNullException>(() => HtmlElementFactory.Create(null));
        }

        /// <summary>
        /// Both 'kbd' and the legacy 'kdb' map to <see cref="HtmlElementTextSemanticsKbd"/>.
        /// </summary>
        [Fact]
        public void KbdTag_MapsToKbdElement()
        {
            Assert.IsType<HtmlElementTextSemanticsKbd>(HtmlElementFactory.Create("kbd"));
            Assert.IsType<HtmlElementTextSemanticsKbd>(HtmlElementFactory.Create("kdb"));
        }

        /// <summary>
        /// The 'keygen' tag is resolved to <see cref="HtmlElementFormKeygen"/>.
        /// </summary>
        [Fact]
        public void KnownTag_Keygen_ReturnsCorrectType()
        {
            var element = HtmlElementFactory.Create("keygen");

            Assert.IsType<HtmlElementFormKeygen>(element);
        }

        /// <summary>
        /// The 'command' tag is resolved to <see cref="HtmlElementInteractiveCommand"/>.
        /// </summary>
        [Fact]
        public void KnownTag_Command_ReturnsCorrectType()
        {
            var element = HtmlElementFactory.Create("command");

            Assert.IsType<HtmlElementInteractiveCommand>(element);
        }
    }
}
