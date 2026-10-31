using WebExpress.WebCore.WebHtml;

namespace WebExpress.WebCore.Test.Html
{
    /// <summary>
    /// Unit tests for Css utility methods.
    /// </summary>
    public class UnitTestCss
    {
        /// <summary>
        /// Tests concatenating css classes.
        /// </summary>
        [Theory]
        [InlineData(null, null, null, "")]
        [InlineData("btn", null, null, "btn")]
        [InlineData("btn", "btn-primary", null, "btn btn-primary")]
        [InlineData("btn", "btn", "btn-primary", "btn btn-primary")]
        [InlineData("btn", " ", "btn-primary", "btn btn-primary")]
        public void Concatenate(string a, string b, string c, string expected)
        {
            // act
            var css = Css.Concatenate(a, b, c);

            // validation
            Assert.Equal(expected, css);
        }

        /// <summary>
        /// Tests removing css classes.
        /// </summary>
        [Theory]
        [InlineData("btn btn-primary", "btn-primary", null, "btn")]
        [InlineData("btn btn-primary btn-lg", "btn-primary", "btn-lg", "btn")]
        [InlineData("btn btn-primary", "btn-secondary", null, "btn btn-primary")]
        [InlineData("btn", "btn", null, "")]
        public void Remove(string css, string remove1, string remove2, string expected)
        {
            // act
            var result = Css.Remove(css, remove1, remove2);

            // validation
            Assert.Equal(expected, result);
        }

        /// <summary>
        /// Tests replacing a css class.
        /// </summary>
        [Theory]
        [InlineData("btn btn-primary", "btn-primary", null, "btn")]
        [InlineData("btn btn-primary btn-lg", "btn-primary", "btn-lg", "btn btn-lg")]
        [InlineData("btn btn-primary", "btn-secondary", null, "btn btn-primary")]
        [InlineData("btn", "btn", null, "")]
        [InlineData("btn", null, null, "btn")]
        public void Replace(string css, string remove, string add, string expected)
        {
            // act
            var result = Css.Replace(css, remove, add);

            // validation
            Assert.Equal(expected, result);
        }

        /// <summary>
        /// Tests replacing a css class when the remove class is not present.
        /// </summary>
        [Fact]
        public void ReplaceWithoutRemoveMatch()
        {
            // act
            var css = Css.Replace("btn", "btn-primary", "btn-outline-primary");

            // validation
            Assert.Equal("btn btn-outline-primary", css);
        }
    }
}
