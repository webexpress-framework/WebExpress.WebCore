using System.Linq;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebCore.WebHtml.Parser;

namespace WebExpress.WebCore.Test.Html.Parser
{
    /// <summary>
    /// Unit tests for the <see cref="HtmlParser"/> class.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestHtmlParser
    {
        private static readonly HtmlParser Parser = new();

        // ------------------------------------------------------------------
        // Simple elements
        // ------------------------------------------------------------------

        /// <summary>
        /// A simple element is correctly reconstructed.
        /// </summary>
        [Fact]
        public void SimpleElement_Div_IsReconstructed()
        {
            var nodes = Parser.Parse("<div></div>");
            var div = nodes.OfType<HtmlElementTextContentDiv>().Single();

            Assert.NotNull(div);
        }

        /// <summary>
        /// An element with a class attribute retains its attribute.
        /// </summary>
        [Fact]
        public void ElementWithClass_RetainsAttribute()
        {
            var nodes = Parser.Parse("<div class=\"container\"></div>");
            var div = nodes.OfType<HtmlElementTextContentDiv>().Single();

            Assert.Equal("container", div.Class);
        }

        /// <summary>
        /// An element with an id attribute retains its attribute.
        /// </summary>
        [Fact]
        public void ElementWithId_RetainsAttribute()
        {
            var nodes = Parser.Parse("<p id=\"intro\">text</p>");
            var p = nodes.OfType<HtmlElementTextContentP>().Single();

            Assert.Equal("intro", p.Id);
        }

        // ------------------------------------------------------------------
        // Nested structures
        // ------------------------------------------------------------------

        /// <summary>
        /// A nested element hierarchy is correctly reconstructed.
        /// </summary>
        [Fact]
        public void NestedElements_AreReconstructed()
        {
            var nodes = Parser.Parse("<div><span>text</span></div>");
            var div = nodes.OfType<HtmlElementTextContentDiv>().Single();
            var span = div.Elements.OfType<HtmlElementTextSemanticsSpan>().Single();

            Assert.NotNull(span);
        }

        /// <summary>
        /// A deeply nested structure is correctly reconstructed.
        /// </summary>
        [Fact]
        public void DeepNesting_IsReconstructed()
        {
            var nodes = Parser.Parse("<ul><li><span>item</span></li></ul>");
            var ul = nodes.OfType<HtmlElementTextContentUl>().Single();
            var li = ul.Elements.OfType<HtmlElementTextContentLi>().Single();
            var span = li.Elements.OfType<HtmlElementTextSemanticsSpan>().Single();

            Assert.NotNull(span);
        }

        // ------------------------------------------------------------------
        // Text nodes
        // ------------------------------------------------------------------

        /// <summary>
        /// A text node inside an element is preserved.
        /// </summary>
        [Fact]
        public void TextNode_IsPreserved()
        {
            var nodes = Parser.Parse("<p>Hello World</p>");
            var p = nodes.OfType<HtmlElementTextContentP>().Single();
            var text = p.Elements.OfType<HtmlText>().Single();

            Assert.Equal("Hello World", text.Value);
        }

        /// <summary>
        /// A bare text node at the top level is returned as a text node.
        /// </summary>
        [Fact]
        public void BareText_ReturnsTextNode()
        {
            var nodes = Parser.Parse("Hello");
            var text = nodes.OfType<HtmlText>().Single();

            Assert.Equal("Hello", text.Value);
        }

        // ------------------------------------------------------------------
        // Self-closing tags
        // ------------------------------------------------------------------

        /// <summary>
        /// A self-closing &lt;br/&gt; tag produces a <see cref="HtmlElementTextSemanticsBr"/>.
        /// </summary>
        [Fact]
        public void SelfClosingBr_IsReconstructed()
        {
            var nodes = Parser.Parse("<br/>");
            var br = nodes.OfType<HtmlElementTextSemanticsBr>().Single();

            Assert.NotNull(br);
        }

        /// <summary>
        /// A void &lt;img&gt; tag (no trailing slash) produces a <see cref="HtmlElementMultimediaImg"/>.
        /// </summary>
        [Fact]
        public void VoidImg_IsReconstructed()
        {
            var nodes = Parser.Parse("<img src=\"photo.png\" alt=\"photo\">");
            var img = nodes.OfType<HtmlElementMultimediaImg>().Single();

            Assert.Equal("photo.png", img.Src);
            Assert.Equal("photo", img.Alt);
        }

        // ------------------------------------------------------------------
        // Attributes
        // ------------------------------------------------------------------

        /// <summary>
        /// Boolean attributes are applied to the element.
        /// </summary>
        [Fact]
        public void BooleanAttribute_IsApplied()
        {
            var nodes = Parser.Parse("<input disabled>");
            var input = nodes.OfType<HtmlElementFieldInput>().Single();

            Assert.True(input.HasUserAttribute("disabled"));
        }

        /// <summary>
        /// data-* attributes are preserved on the element.
        /// </summary>
        [Fact]
        public void DataAttribute_IsPreserved()
        {
            var nodes = Parser.Parse("<div data-toggle=\"modal\"></div>");
            var div = nodes.OfType<HtmlElementTextContentDiv>().Single();

            Assert.Equal("modal", div.GetUserAttribute("data-toggle"));
        }

        /// <summary>
        /// ARIA attributes are preserved on the element.
        /// </summary>
        [Fact]
        public void AriaAttribute_IsPreserved()
        {
            var nodes = Parser.Parse("<button aria-label=\"Close\"></button>");
            var btn = nodes.OfType<HtmlElementFieldButton>().Single();

            Assert.Equal("Close", btn.GetUserAttribute("aria-label"));
        }

        // ------------------------------------------------------------------
        // Comments
        // ------------------------------------------------------------------

        /// <summary>
        /// An HTML comment is reconstructed as a <see cref="HtmlComment"/> node.
        /// </summary>
        [Fact]
        public void Comment_IsReconstructed()
        {
            var nodes = Parser.Parse("<!-- remark -->");
            var comment = nodes.OfType<HtmlComment>().Single();

            Assert.Equal("remark", comment.Text);
        }

        // ------------------------------------------------------------------
        // DOCTYPE
        // ------------------------------------------------------------------

        /// <summary>
        /// A DOCTYPE declaration does not produce a node in the tree (it is
        /// informational only).
        /// </summary>
        [Fact]
        public void Doctype_ProducesNoNode()
        {
            var nodes = Parser.Parse("<!DOCTYPE html><html></html>");

            Assert.DoesNotContain(nodes, n => n is HtmlText t && t.Value.Contains("DOCTYPE"));
        }

        // ------------------------------------------------------------------
        // Unknown tags
        // ------------------------------------------------------------------

        /// <summary>
        /// An unknown tag is mapped to a generic <see cref="HtmlElement"/>.
        /// </summary>
        [Fact]
        public void UnknownTag_MapsToGenericElement()
        {
            var nodes = Parser.Parse("<x-widget foo=\"bar\"></x-widget>");
            var element = nodes.OfType<HtmlElement>().Single();

            Assert.Equal("bar", element.GetUserAttribute("foo"));
        }

        // ------------------------------------------------------------------
        // Malformed HTML
        // ------------------------------------------------------------------

        /// <summary>
        /// An unclosed tag is handled gracefully and the element is still returned.
        /// </summary>
        [Fact]
        public void UnclosedTag_IsHandledGracefully()
        {
            var nodes = Parser.Parse("<div><p>text");

            var div = nodes.OfType<HtmlElementTextContentDiv>().Single();
            Assert.NotNull(div);
        }

        /// <summary>
        /// Parsing an empty string does not throw.
        /// </summary>
        [Fact]
        public void EmptyInput_ReturnsEmptyList()
        {
            var nodes = Parser.Parse("");

            Assert.Empty(nodes);
        }

        /// <summary>
        /// Passing <c>null</c> to <see cref="HtmlParser.Parse"/> throws
        /// <see cref="System.ArgumentNullException"/>.
        /// </summary>
        [Fact]
        public void NullInput_Throws()
        {
            Assert.Throws<System.ArgumentNullException>(() => Parser.Parse(null));
        }

        // ------------------------------------------------------------------
        // Round-trip tests
        // ------------------------------------------------------------------

        /// <summary>
        /// Parsing the HTML produced by the renderer reconstructs the same
        /// element type.
        /// </summary>
        [Fact]
        public void RoundTrip_SimpleDiv_PreservesType()
        {
            // arrange
            var original = new HtmlElementTextContentDiv();
            original.Id = "main";
            original.AddClass("container");

            // act
            var html = original.ToString().Trim();
            var parsed = Parser.Parse(html);

            var restored = parsed.OfType<HtmlElementTextContentDiv>().Single();

            // validation
            Assert.Equal(original.Id, restored.Id);
            Assert.Equal(original.Class, restored.Class);
        }

        /// <summary>
        /// Parsing the HTML produced by the renderer for a nested structure
        /// reconstructs the hierarchy.
        /// </summary>
        [Fact]
        public void RoundTrip_NestedStructure_PreservesHierarchy()
        {
            // arrange
            var original = new HtmlElementTextContentDiv(
                new HtmlElementTextSemanticsSpan(new HtmlText("hello"))
            );

            // act
            var html = original.ToString().Trim();
            var parsed = Parser.Parse(html);

            var div = parsed.OfType<HtmlElementTextContentDiv>().Single();
            var span = div.Elements.OfType<HtmlElementTextSemanticsSpan>().Single();
            var text = span.Elements.OfType<HtmlText>().Single();

            // validation
            Assert.Equal("hello", text.Value);
        }

        /// <summary>
        /// Rendering the parsed HTML of an &lt;img&gt; element produces equivalent HTML.
        /// </summary>
        [Fact]
        public void RoundTrip_Img_ProducesEquivalentHtml()
        {
            // arrange
            var original = new HtmlElementMultimediaImg
            {
                Src = "logo.png",
                Alt = "Logo"
            };

            // act
            var html = original.ToString().Trim();
            var parsed = Parser.Parse(html);

            var img = parsed.OfType<HtmlElementMultimediaImg>().Single();
            var restoredHtml = img.ToString().Trim();

            // validation
            Assert.Equal(html, restoredHtml);
        }

        // ------------------------------------------------------------------
        // Additional tests
        // ------------------------------------------------------------------

        /// <summary>
        /// ParseSingle returns the first node.
        /// </summary>
        [Fact]
        public void ParseSingle_ReturnsFirstNode()
        {
            var node = Parser.ParseSingle("<div></div>");

            Assert.IsType<HtmlElementTextContentDiv>(node);
        }

        /// <summary>
        /// ParseSingle returns null for an empty input.
        /// </summary>
        [Fact]
        public void ParseSingle_EmptyInput_ReturnsNull()
        {
            var node = Parser.ParseSingle("");

            Assert.Null(node);
        }

        /// <summary>
        /// An element with an inline style attribute retains its value.
        /// </summary>
        [Fact]
        public void InlineStyleAttribute_IsPreserved()
        {
            var nodes = Parser.Parse("<div style=\"color: red;\"></div>");
            var div = nodes.OfType<HtmlElementTextContentDiv>().Single();

            Assert.Equal("color: red;", div.Style);
        }

        /// <summary>
        /// A table structure with thead, tbody, and rows is correctly reconstructed.
        /// </summary>
        [Fact]
        public void TableStructure_IsReconstructed()
        {
            var nodes = Parser.Parse("<table><thead><tr><th>Header</th></tr></thead><tbody><tr><td>Cell</td></tr></tbody></table>");
            var table = nodes.OfType<HtmlElementTableTable>().Single();
            var thead = table.Elements.OfType<HtmlElementTableThead>().Single();
            var tbody = table.Elements.OfType<HtmlElementTableTbody>().Single();

            Assert.NotNull(thead);
            Assert.NotNull(tbody);
        }

        /// <summary>
        /// Multiple top-level elements are all returned.
        /// </summary>
        [Fact]
        public void MultipleRoots_AreAllReturned()
        {
            var nodes = Parser.Parse("<p>one</p><p>two</p>");

            Assert.Equal(2, nodes.Count);
            Assert.All(nodes, n => Assert.IsType<HtmlElementTextContentP>(n));
        }

        /// <summary>
        /// A mismatched end tag is handled gracefully without throwing.
        /// </summary>
        [Fact]
        public void MismatchedEndTag_IsHandledGracefully()
        {
            var nodes = Parser.Parse("<div><span>text</div>");

            var div = nodes.OfType<HtmlElementTextContentDiv>().Single();
            Assert.NotNull(div);
        }

        /// <summary>
        /// Mixed text and element children are preserved in order.
        /// </summary>
        [Fact]
        public void MixedContent_TextAndElements_ArePreserved()
        {
            var nodes = Parser.Parse("<p>Hello <strong>World</strong>!</p>");
            var p = nodes.OfType<HtmlElementTextContentP>().Single();

            Assert.Equal(3, p.Elements.Count());
        }

        /// <summary>
        /// Roundtrip of a styled element preserves the style attribute.
        /// </summary>
        [Fact]
        public void RoundTrip_StyleAttribute_IsPreserved()
        {
            // arrange
            var original = new HtmlElementTextContentDiv();
            original.Style = "color: red;";

            // act
            var html = original.ToString().Trim();
            var parsed = Parser.Parse(html);

            var restored = parsed.OfType<HtmlElementTextContentDiv>().Single();

            // validation
            Assert.Equal("color: red;", restored.Style);
        }

        /// <summary>
        /// Roundtrip of an anchor element preserves href and text content.
        /// </summary>
        [Fact]
        public void RoundTrip_Anchor_PreservesHrefAndText()
        {
            // arrange
            var original = new HtmlElementTextSemanticsA(new HtmlText("click me"));
            original.Href = "https://example.com";

            // act
            var html = original.ToString().Trim();
            var parsed = Parser.Parse(html);

            var a = parsed.OfType<HtmlElementTextSemanticsA>().Single();
            var text = a.Elements.OfType<HtmlText>().Single();

            // validation
            Assert.Equal("https://example.com", a.Href);
            Assert.Equal("click me", text.Value);
        }

        /// <summary>
        /// A form with input fields is correctly reconstructed.
        /// </summary>
        [Fact]
        public void FormWithInputs_IsReconstructed()
        {
            var nodes = Parser.Parse("<form action=\"/submit\"><input type=\"text\" name=\"q\"></form>");
            var form = nodes.OfType<HtmlElementFormForm>().Single();
            var input = form.Elements.OfType<HtmlElementFieldInput>().Single();

            Assert.NotNull(input);
        }

        /// <summary>
        /// The kbd tag (standard HTML) maps to HtmlElementTextSemanticsKdb.
        /// </summary>
        [Fact]
        public void KbdTag_MapsToKdbElement()
        {
            var nodes = Parser.Parse("<kbd>Ctrl+C</kbd>");
            var kbd = nodes.OfType<HtmlElementTextSemanticsKdb>().Single();

            Assert.NotNull(kbd);
        }

        /// <summary>
        /// Elements with content models keep their children instead of ending the parse at
        /// their closing tag.
        /// </summary>
        [Theory]
        [InlineData("<fieldset><legend>Address</legend></fieldset>")]
        [InlineData("<audio><source src=\"a.ogg\"></audio>")]
        [InlineData("<video><source src=\"a.mp4\"></video>")]
        [InlineData("<canvas><p>fallback</p></canvas>")]
        [InlineData("<map name=\"m\"><area href=\"/a\"></map>")]
        [InlineData("<noscript><p>enable scripting</p></noscript>")]
        public void ContainerElement_KeepsChildrenAndFollowingSibling(string markup)
        {
            var nodes = Parser.Parse(markup + "<p>after</p>");
            var container = nodes.OfType<HtmlElement>().First();

            Assert.Equal(2, nodes.Count());
            Assert.Single(container.Elements);
            Assert.IsType<HtmlElementTextContentP>(nodes.Last());
        }

        /// <summary>
        /// Void elements are written back without a closing tag and leave their
        /// following sibling outside.
        /// </summary>
        [Theory]
        [InlineData("<embed src=\"a.swf\">", typeof(HtmlElementEmbeddedEmbed))]
        [InlineData("<keygen name=\"k\">", typeof(HtmlElementFormKeygen))]
        public void VoidElement_RoundTripsWithoutClosingTag(string markup, System.Type type)
        {
            var nodes = Parser.Parse(markup + "<p>after</p>");
            var element = nodes.First();

            Assert.IsType(type, element);
            Assert.Empty(((HtmlElement)element).Elements);
            Assert.DoesNotContain("</", element.ToString());
        }

        /// <summary>
        /// A col inside a colgroup is written back as a void element.
        /// </summary>
        [Fact]
        public void ColInColgroup_RoundTripsWithoutClosingTag()
        {
            var nodes = Parser.Parse("<colgroup><col span=\"2\"><col></colgroup>");
            var colgroup = nodes.OfType<HtmlElementTableColgroup>().Single();

            Assert.Equal(2, colgroup.Elements.OfType<HtmlElementTableCol>().Count());
            Assert.DoesNotContain("</col>", colgroup.ToString());
        }
    }
}
