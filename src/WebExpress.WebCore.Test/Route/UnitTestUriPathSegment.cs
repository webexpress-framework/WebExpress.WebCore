using WebExpress.WebCore.Test.Fixture;
using WebExpress.WebCore.WebUri;

namespace WebExpress.WebCore.Test.Route
{
    /// <summary>
    /// Tests the path segment.
    /// </summary>
    public class UnitTestUriPathSegment
    {
        /// <summary>
        /// Test the constant segment.
        /// </summary>
        [Theory]
        [InlineData(null, "<null>", null)]
        [InlineData("abc", "abc", null)]
        public void Constant(string value, string expected, string displayText)
        {
            // arrange
            var renderContet = UnitTestFixture.CrerateRenderContextMock();

            // act
            var segment = new UriPathSegmentConstant(value);

            // validation
            Assert.Equal(expected, segment.ToString());
            Assert.Equal(displayText, segment.GetDisplayText(renderContet));
        }

        /// <summary>
        /// Test the int segment.
        /// </summary>
        [Theory]
        [InlineData(null, "${testparametera}", null)]
        [InlineData("123", "123", "123")]
        public void Int(string value, string expected, string displayText)
        {
            // arrange
            var renderContet = UnitTestFixture.CrerateRenderContextMock();

            // act
            var segment = new UriPathSegmentVariableInt<TestParameterA>()
            {
                Value = value
            };

            // validation
            Assert.Equal(expected, segment.ToString());
            Assert.Equal(displayText, segment.GetDisplayText(renderContet));
        }

        /// <summary>
        /// Test the uint segment.
        /// </summary>
        [Theory]
        [InlineData(null, "${testparametera}", null)]
        [InlineData("123", "123", "123")]
        public void UInt(string value, string expected, string displayText)
        {
            // arrange
            var renderContet = UnitTestFixture.CrerateRenderContextMock();

            // act
            var segment = new UriPathSegmentVariableUInt<TestParameterA>()
            {
                Value = value
            };

            // validation
            Assert.Equal(expected, segment.ToString());
            Assert.Equal(displayText, segment.GetDisplayText(renderContet));
        }

        /// <summary>
        /// Test the double segment.
        /// </summary>
        [Theory]
        [InlineData(null, "${testparametera}", null)]
        [InlineData("123", "123", "123")]
        public void Double(string value, string expected, string displayText)
        {
            // arrange
            var renderContet = UnitTestFixture.CrerateRenderContextMock();

            // act
            var segment = new UriPathSegmentVariableDouble<TestParameterA>()
            {
                Value = value
            };

            // validation
            Assert.Equal(expected, segment.ToString());
            Assert.Equal(displayText, segment.GetDisplayText(renderContet));
        }

        /// <summary>
        /// Test the guid segment.
        /// </summary>
        [Theory]
        [InlineData(null, "${testparametera}", null)]
        [InlineData("123", "123", "")]
        public void Guid(string value, string expected, string displayText)
        {
            // arrange
            var renderContet = UnitTestFixture.CrerateRenderContextMock();

            // act
            var segment = new UriPathSegmentVariableGuid<TestParameterA>()
            {
                Value = value
            };

            // validation
            Assert.Equal(expected, segment.ToString());
            Assert.Equal(displayText, segment.GetDisplayText(renderContet));
        }

        /// <summary>
        /// Test the regex segment.
        /// </summary>
        [Theory]
        [InlineData(null, "${testparametera}", null)]
        [InlineData("123", "123", "123")]
        public void Regex(string value, string expected, string displayText)
        {
            // arrange
            var renderContet = UnitTestFixture.CrerateRenderContextMock();

            // act
            var segment = new UriPathSegmentVariableRegex<TestParameterA>(".*")
            {
                Value = value
            };

            // validation
            Assert.Equal(expected, segment.ToString());
            Assert.Equal(displayText, segment.GetDisplayText(renderContet));
        }

        /// <summary>
        /// Test the regex segment.
        /// </summary>
        [Theory]
        [InlineData(null, "${testparametera}", null)]
        [InlineData("123", "123", "123")]
        public void String(string value, string expected, string displayText)
        {
            // arrange
            var renderContet = UnitTestFixture.CrerateRenderContextMock();

            // act
            var segment = new UriPathSegmentVariableString<TestParameterA>(".*")
            {
                Value = value
            };

            // validation
            Assert.Equal(expected, segment.ToString());
            Assert.Equal(displayText, segment.GetDisplayText(renderContet));
        }
    }
}
