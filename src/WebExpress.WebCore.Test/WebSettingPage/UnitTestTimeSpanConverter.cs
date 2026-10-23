using System;
using System.Globalization;
using WebExpress.WebCore.WebSettingPage.Model;

namespace WebExpress.WebCore.Test.WebSettingPage
{
    /// <summary>
    /// Tests the converter that formats a time span for the setting pages and reads the
    /// formatted string back.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestTimeSpanConverter
    {
        /// <summary>
        /// A span survives the round trip through the formatted string.
        /// </summary>
        [Theory]
        [InlineData("00:00:00")]
        [InlineData("00:00:01.250")]
        [InlineData("1.02:03:04.005")]
        [InlineData("12:34:56")]
        [InlineData("-1.02:03:04.005")]
        public void ConvertBackReadsWhatConvertWrote(string literal)
        {
            // arrange
            var converter = new TimeSpanConverter();
            var expected = TimeSpan.Parse(literal, CultureInfo.InvariantCulture);

            // act
            var formatted = converter.Convert(expected, typeof(string), null, null);
            var actual = converter.ConvertBack(formatted, typeof(TimeSpan), null, null);

            // validation
            Assert.Equal(expected, actual);
        }

        /// <summary>
        /// A shortened form names only the units it needs, and a plain literal is accepted as
        /// it is.
        /// </summary>
        [Theory]
        [InlineData("5m", 0, 0, 5, 0, 0)]
        [InlineData("2h 30m", 0, 2, 30, 0, 0)]
        [InlineData("250ms", 0, 0, 0, 0, 250)]
        [InlineData("1.02:03:04", 1, 2, 3, 4, 0)]
        public void ConvertBackAcceptsShortenedAndLiteralForms(string value, int days, int hours, int minutes, int seconds, int milliseconds)
        {
            // act
            var actual = new TimeSpanConverter().ConvertBack(value, typeof(TimeSpan), null, null);

            // validation
            Assert.Equal(new TimeSpan(days, hours, minutes, seconds, milliseconds), actual);
        }

        /// <summary>
        /// Nothing is read into a null, and a string that is neither form is rejected rather
        /// than silently turned into zero.
        /// </summary>
        [Fact]
        public void ConvertBackRejectsWhatItCannotRead()
        {
            // arrange
            var converter = new TimeSpanConverter();

            // validation
            Assert.Null(converter.ConvertBack(null, typeof(TimeSpan), null, null));
            Assert.Throws<FormatException>(() => converter.ConvertBack("", typeof(TimeSpan), null, null));
            Assert.Throws<FormatException>(() => converter.ConvertBack("soon", typeof(TimeSpan), null, null));
        }
    }
}
