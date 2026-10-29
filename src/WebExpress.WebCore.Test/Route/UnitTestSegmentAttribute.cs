using WebExpress.WebCore.WebAttribute;

namespace WebExpress.WebCore.Test.Route
{
    /// <summary>
    /// Tests the segment attribute.
    /// </summary>
    public class UnitTestSegmentAttribute
    {
        /// <summary>
        /// Test the constant segment.
        /// </summary>
        [Theory]
        [InlineData(null, "<null>")]
        [InlineData("abc", "abc")]
        public void Constant(string name, string expected)
        {
            // arrange
            var attribute = new SegmentAttribute(name);

            // act
            var segment = attribute.ToPathSegment();

            // validation
            Assert.Equal(expected, segment.ToString());
        }

        /// <summary>
        /// Test the double segment attribute.
        /// </summary>
        [Theory]
        [InlineData("${testparametera}")]
        public void Double(string expected)
        {
            // arrange
            var attribute = new SegmentDoubleAttribute<TestParameterA>("");

            // act
            var segment = attribute.ToPathSegment();

            // validation
            Assert.Equal(expected, segment.ToString());
        }

        /// <summary>
        /// Test the guid segment attribute.
        /// </summary>
        [Theory]
        [InlineData("${testparametera}")]
        public void Guid(string expected)
        {
            // arrange
            var attribute = new SegmentGuidAttribute<TestParameterA>();

            // act
            var segment = attribute.ToPathSegment();

            // validation
            Assert.Equal(expected, segment.ToString());
        }

        /// <summary>
        /// Test the int segment attribute.
        /// </summary>
        [Theory]
        [InlineData("${testparametera}")]
        public void Int(string expected)
        {
            // arrange
            var attribute = new SegmentIntAttribute<TestParameterA>();

            // act
            var segment = attribute.ToPathSegment();

            // validation
            Assert.Equal(expected, segment.ToString());
        }

        /// <summary>
        /// Test the regex segment attribute.
        /// </summary>
        [Theory]
        [InlineData(".*", "${testparametera}")]
        public void Regex(string regex, string expected)
        {
            // arrange
            var attribute = new SegmentRegexAttribute<TestParameterA>(regex);

            // act
            var segment = attribute.ToPathSegment();

            // validation
            Assert.Equal(expected, segment.ToString());
        }

        /// <summary>
        /// Test the string segment attribute.
        /// </summary>
        [Theory]
        [InlineData("${testparametera}")]
        public void String(string expected)
        {
            // arrange
            var attribute = new SegmentStringAttribute<TestParameterA>();

            // act
            var segment = attribute.ToPathSegment();

            // validation
            Assert.Equal(expected, segment.ToString());
        }

        /// <summary>
        /// Test the uint segment attribute.
        /// </summary>
        [Theory]
        [InlineData("${testparametera}")]
        public void UInt(string expected)
        {
            // arrange
            var attribute = new SegmentUIntAttribute<TestParameterA>();

            // act
            var segment = attribute.ToPathSegment();

            // validation
            Assert.Equal(expected, segment.ToString());
        }
    }
}
