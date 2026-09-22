using System;
using System.Collections.Generic;

namespace WebExpress.WebCore.WebHtml.Parser
{
    /// <summary>
    /// Maps HTML tag names to their corresponding <see cref="HtmlElement"/> subclass instances.
    /// </summary>
    /// <remarks>
    /// When a tag name is not recognised, the factory returns a generic
    /// <see cref="HtmlElement"/> instance whose <c>ElementName</c> is preserved so
    /// that the tag name survives a round-trip through the renderer.
    /// </remarks>
    public class HtmlElementFactory
    {
        private static readonly Dictionary<string, Func<HtmlElement>> _registry =
            new(StringComparer.OrdinalIgnoreCase)
            {
                // Root
                ["html"] = () => new HtmlElementRootHtml(),

                // Metadata
                ["head"] = () => new HtmlElementMetadataHead(),
                ["base"] = () => new HtmlElementMetadataBase(),
                ["link"] = () => new HtmlElementMetadataLink(),
                ["meta"] = () => new HtmlElementMetadataMeta(),
                ["style"] = () => new HtmlElementMetadataStyle(),
                ["title"] = () => new HtmlElementMetadataTitle(),

                // Scripting
                // parsed markup may stem from user input; a nonce would let a stored script run
                ["script"] = () => new HtmlElementScriptingScript() { Trusted = false },
                ["noscript"] = () => new HtmlElementScriptingNoscript(),
                ["canvas"] = () => new HtmlElementScriptingCanvas(),

                // Sections
                ["body"] = () => new HtmlElementSectionBody(),
                ["address"] = () => new HtmlElementSectionAddress(),
                ["article"] = () => new HtmlElementSectionArticle(),
                ["aside"] = () => new HtmlElementSectionAside(),
                ["footer"] = () => new HtmlElementSectionFooter(),
                ["h1"] = () => new HtmlElementSectionH1(),
                ["h2"] = () => new HtmlElementSectionH2(),
                ["h3"] = () => new HtmlElementSectionH3(),
                ["h4"] = () => new HtmlElementSectionH4(),
                ["h5"] = () => new HtmlElementSectionH5(),
                ["h6"] = () => new HtmlElementSectionH6(),
                ["header"] = () => new HtmlElementSectionHeader(),
                ["main"] = () => new HtmlElementSectionMain(),
                ["nav"] = () => new HtmlElementSectionNav(),
                ["section"] = () => new HtmlElementSectionSection(),

                // Text content
                ["blockquote"] = () => new HtmlElementTextContentBlockquote(),
                ["dd"] = () => new HtmlElementTextContentDd(),
                ["div"] = () => new HtmlElementTextContentDiv(),
                ["dl"] = () => new HtmlElementTextContentDl(),
                ["dt"] = () => new HtmlElementTextContentDt(),
                ["figcaption"] = () => new HtmlElementTextContentFigcaption(),
                ["figure"] = () => new HtmlElementTextContentFigure(),
                ["hr"] = () => new HtmlElementTextContentHr(),
                ["li"] = () => new HtmlElementTextContentLi(),
                ["ol"] = () => new HtmlElementTextContentOl(),
                ["p"] = () => new HtmlElementTextContentP(),
                ["pre"] = () => new HtmlElementTextContentPre(),
                ["ul"] = () => new HtmlElementTextContentUl(),

                // Inline text semantics
                ["a"] = () => new HtmlElementTextSemanticsA(),
                ["abbr"] = () => new HtmlElementTextSemanticsAbbr(),
                ["b"] = () => new HtmlElementTextSemanticsB(),
                ["bdi"] = () => new HtmlElementTextSemanticsBdi(),
                ["bdo"] = () => new HtmlElementTextSemanticsBdo(),
                ["br"] = () => new HtmlElementTextSemanticsBr(),
                ["cite"] = () => new HtmlElementTextSemanticsCite(),
                ["code"] = () => new HtmlElementTextSemanticsCode(),
                ["data"] = () => new HtmlElementTextSemanticsData(),
                ["dfn"] = () => new HtmlElementTextSemanticsDfn(),
                ["em"] = () => new HtmlElementTextSemanticsEm(),
                ["i"] = () => new HtmlElementTextSemanticsI(),
                // The standard HTML element is <kbd>, but the existing class uses "kdb" as
                // its element name.  Both spellings are mapped so that the parser handles
                // real-world HTML (<kbd>) as well as the project's own renderer output (<kdb>).
                ["kbd"] = () => new HtmlElementTextSemanticsKdb(),
                ["kdb"] = () => new HtmlElementTextSemanticsKdb(),
                // 'kbd' is the correct HTML tag name; 'kdb' mirrors the existing class typo.
                ["kbd"] = () => new HtmlElementTextSemanticsKdb(),
                ["mark"] = () => new HtmlElementTextSemanticsMark(),
                ["q"] = () => new HtmlElementTextSemanticsQ(),
                ["rp"] = () => new HtmlElementTextSemanticsRp(),
                ["rt"] = () => new HtmlElementTextSemanticsRt(),
                ["ruby"] = () => new HtmlElementTextSemanticsRuby(),
                ["s"] = () => new HtmlElementTextSemanticsS(),
                ["samp"] = () => new HtmlElementTextSemanticsSamp(),
                ["small"] = () => new HtmlElementTextSemanticsSmall(),
                ["span"] = () => new HtmlElementTextSemanticsSpan(),
                ["strong"] = () => new HtmlElementTextSemanticsStrong(),
                ["sub"] = () => new HtmlElementTextSemanticsSub(),
                ["sup"] = () => new HtmlElementTextSemanticsSup(),
                ["time"] = () => new HtmlElementTextSemanticsTime(),
                ["u"] = () => new HtmlElementTextSemanticsU(),
                ["var"] = () => new HtmlElementTextSemanticsVar(),
                ["wbr"] = () => new HtmlElementTextSemanticsWbr(),

                // Edits
                ["del"] = () => new HtmlElementEditDel(),
                ["ins"] = () => new HtmlElementEditIns(),

                // Embedded content
                ["embed"] = () => new HtmlElementEmbeddedEmbed(),
                ["iframe"] = () => new HtmlElementEmbeddedIframe(),
                ["object"] = () => new HtmlElementEmbeddedObject(),
                ["param"] = () => new HtmlElementEmbeddedParam(),
                ["picture"] = () => new HtmlElementEmbeddedPicture(),
                ["source"] = () => new HtmlElementEmbeddedSource(),

                // Multimedia
                ["area"] = () => new HtmlElementMultimediaArea(),
                ["audio"] = () => new HtmlElementMultimediaAudio(),
                ["img"] = () => new HtmlElementMultimediaImg(),
                ["map"] = () => new HtmlElementMultimediaMap(),
                ["math"] = () => new HtmlElementMultimediaMath(),
                ["svg"] = () => new HtmlElementMultimediaSvg(),
                ["track"] = () => new HtmlElementMultimediaTrack(),
                ["video"] = () => new HtmlElementMultimediaVideo(),

                // Table
                ["caption"] = () => new HtmlElementTableCaption(),
                ["col"] = () => new HtmlElementTableCol(),
                ["colgroup"] = () => new HtmlElementTableColgroup(),
                ["table"] = () => new HtmlElementTableTable(),
                ["tbody"] = () => new HtmlElementTableTbody(),
                ["td"] = () => new HtmlElementTableTd(),
                ["tfoot"] = () => new HtmlElementTableTfoot(),
                ["th"] = () => new HtmlElementTableTh(),
                ["thead"] = () => new HtmlElementTableThead(),
                ["tr"] = () => new HtmlElementTableTr(),

                // Forms
                ["button"] = () => new HtmlElementFieldButton(),
                ["input"] = () => new HtmlElementFieldInput(),
                ["label"] = () => new HtmlElementFieldLabel(),
                ["legend"] = () => new HtmlElementFieldLegend(),
                ["select"] = () => new HtmlElementFieldSelect(),
                ["datalist"] = () => new HtmlElementFormDatalist(),
                ["fieldset"] = () => new HtmlElementFormFieldset(),
                ["form"] = () => new HtmlElementFormForm(),
                ["keygen"] = () => new HtmlElementFormKeygen(),
                ["meter"] = () => new HtmlElementFormMeter(),
                ["optgroup"] = () => new HtmlElementFormOptgroup(),
                ["option"] = () => new HtmlElementFormOption(),
                ["output"] = () => new HtmlElementFormOutput(),
                ["progress"] = () => new HtmlElementFormProgress(),
                ["textarea"] = () => new HtmlElementFormTextarea(),

                // Interactive
                ["command"] = () => new HtmlElementInteractiveCommand(),
                ["details"] = () => new HtmlElementInteractiveDetails(),
                ["menu"] = () => new HtmlElementInteractiveMenu(),
                ["summary"] = () => new HtmlElementInteractiveSummary(),

                // Web fragments
                ["slot"] = () => new HtmlElementWebFragmentsSlot(),
                ["template"] = () => new HtmlElementWebFragmentsTemplate(),
            };

        /// <summary>
        /// Creates an <see cref="HtmlElement"/> instance for the specified HTML tag name.
        /// </summary>
        /// <param name="tagName">The lower-case HTML tag name (e.g. <c>"div"</c>).</param>
        /// <returns>
        /// An instance of the most specific <see cref="HtmlElement"/> subclass that
        /// corresponds to <paramref name="tagName"/>. If the tag name is unknown, a generic
        /// <see cref="HtmlElement"/> is returned so that the parser remains robust.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="tagName"/> is <c>null</c>.
        /// </exception>
        public static HtmlElement Create(string tagName)
        {
            if (tagName is null)
            {
                throw new ArgumentNullException(nameof(tagName));
            }

            if (_registry.TryGetValue(tagName, out var factory))
            {
                return factory();
            }

            // Unknown tag – return a generic element so parsing remains robust.
            return new HtmlElement(tagName);
        }

        /// <summary>
        /// Returns <c>true</c> if the specified tag name is registered in the factory.
        /// </summary>
        /// <param name="tagName">The HTML tag name to look up (case-insensitive).</param>
        public static bool IsKnown(string tagName) =>
            tagName is not null && _registry.ContainsKey(tagName);
    }
}
