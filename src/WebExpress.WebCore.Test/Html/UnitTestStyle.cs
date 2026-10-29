using WebExpress.WebCore.WebHtml;

namespace WebExpress.WebCore.Test.Html
{
    /// <summary>
    /// Unit tests for Style utility methods.
    /// </summary>
    public class UnitTestStyle
    {
        /// <summary>
        /// Tests concatenating style entries.
        /// </summary>
        [Theory]
        [InlineData(null, null, null, "")]
        [InlineData("color:red;", null, null, "color:red;")]
        [InlineData("color:red;", "background:blue;", null, "color:red; background:blue;")]
        [InlineData("color:red;", "color:red;", "background:blue;", "color:red; background:blue;")]
        [InlineData("color:red;", " ", "background:blue;", "color:red; background:blue;")]
        public void Concatenate(string a, string b, string c, string expected)
        {
            // act
            var style = Style.Concatenate(a, b, c);

            // validation
            Assert.Equal(expected, style);
        }

        /// <summary>
        /// Tests removing style entries.
        /// </summary>
        [Theory]
        [InlineData("color:red; background:blue;", "color:red;", null, "background:blue;")]
        [InlineData("color:red; background:blue; margin:1rem;", "color:red;", "margin:1rem;", "background:blue;")]
        [InlineData("color:red; background:blue;", "border:1px", null, "color:red; background:blue;")]
        [InlineData("color:red;", "color:red;", null, "")]
        public void Remove(string styles, string remove1, string remove2, string expected)
        {
            // act
            var result = Style.Remove(styles, remove1, remove2);

            // validation
            Assert.Equal(expected, result);
        }

        /// <summary>
        /// Tests replacing a style entry.
        /// </summary>
        [Theory]
        [InlineData("color:red; background:blue;", "color:red;", null, "background:blue;")]
        [InlineData("color:red; background:blue; margin:1rem;", "color:red;", "margin:1rem;", "background:blue; margin:1rem;")]
        [InlineData("color:red; background:blue;", "border:1px", null, "color:red; background:blue;")]
        [InlineData("color:red;", "color:red;", null, "")]
        [InlineData("color:red;", null, null, "color:red;")]
        [InlineData("color:red; background:blue;", "color:red;", "background:blue;", "background:blue;")]
        public void Replace(string styles, string remove, string add, string expected)
        {
            // act
            var style = Style.Replace(styles, remove, add);

            // validation
            Assert.Equal(expected, style);
        }

        /// <summary>
        /// Tests replacing a style entry when the remove style is not present.
        /// </summary>
        [Fact]
        public void ReplaceWithoutRemoveMatch()
        {
            // act
            var style = Style.Replace("background:blue;", "color:red;", "color:green;");

            // validation
            Assert.Equal("background:blue; color:green;", style);
        }
    }
}
