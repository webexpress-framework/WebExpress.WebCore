using System.Globalization;
using WebExpress.WebCore.WebJob;

namespace WebExpress.WebCore.Test.Schedule
{
    /// <summary>
    /// Tests the scheduler's clock.
    /// </summary>
    public class UnitTestClock
    {
        /// <summary>
        /// Test the synchronization of the clock.
        /// </summary>
        [Theory]
        [InlineData(0, 0, 0, 0, 0)]
        [InlineData(0, 0, 0, 30, 0)]
        [InlineData(0, 0, -5, 0, 5)]
        [InlineData(0, 0, 5, 0, 0)]
        [InlineData(-1, 0, 0, 0, 24 * 60)]
        [InlineData(-1, -10, 0, 0, (24 * 60) + (10 * 60))]
        public void Synchronize(int? days, int? hours, int? minutes, int? seconds, int expected)
        {
            // arrange
            var dateTime = DateTime.Now;

            if (days.HasValue)
            {
                dateTime = dateTime.AddDays(days.Value);
            }

            if (hours.HasValue)
            {
                dateTime = dateTime.AddHours(hours.Value);
            }

            if (minutes.HasValue)
            {
                dateTime = dateTime.AddMinutes(minutes.Value);
            }

            if (seconds.HasValue)
            {
                dateTime = dateTime.AddSeconds(seconds.Value);
            }

            var clock = new Clock(dateTime);

            // act
            var elapsed = clock.Synchronize();

            Assert.Equal(expected, elapsed.Count());
        }

        /// <summary>
        /// Test the == operator of the clock.
        /// </summary>
        [Theory]
        [InlineData("2020-12-31 23:59:00", "2020-12-31 23:59:00", true)]
        [InlineData("2020-12-31 23:59:00", "2021-01-01 00:00:00", false)]
        public void CompareEquals(string dateTime1, string dateTime2, bool expected)
        {
            // arrange
            var clock1 = new Clock(DateTime.ParseExact(dateTime1, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            var clock2 = new Clock(DateTime.ParseExact(dateTime2, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));

            // act
            Assert.Equal(expected, clock1 == clock2);
        }

        /// <summary>
        /// Test the != operator of the clock.
        /// </summary>
        [Theory]
        [InlineData("2020-12-31 23:59:00", "2020-12-31 23:59:00", false)]
        [InlineData("2020-12-31 23:59:00", "2021-01-01 00:00:00", true)]
        public void CompareInequality(string dateTime1, string dateTime2, bool expected)
        {
            // arrange
            var clock1 = new Clock(DateTime.ParseExact(dateTime1, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            var clock2 = new Clock(DateTime.ParseExact(dateTime2, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));

            // act
            Assert.Equal(expected, clock1 != clock2);
        }

        /// <summary>
        /// Test the less operator of the clock.
        /// </summary>
        [Theory]
        [InlineData("2020-12-31 23:59:00", "2020-12-31 23:59:00", false)]
        [InlineData("2021-01-01 00:00:00", "2020-12-31 23:59:00", false)]
        [InlineData("2020-12-31 23:59:00", "2021-01-01 00:00:00", true)]
        public void CompareLess(string dateTime1, string dateTime2, bool expected)
        {
            // arrange
            var clock1 = new Clock(DateTime.ParseExact(dateTime1, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            var clock2 = new Clock(DateTime.ParseExact(dateTime2, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));

            // act
            Assert.Equal(expected, clock1 < clock2);
        }

        /// <summary>
        /// Test the greater operator of the clock.
        /// </summary>
        [Theory]
        [InlineData("2020-12-31 23:59:00", "2020-12-31 23:59:00", false)]
        [InlineData("2021-01-01 00:00:00", "2020-12-31 23:59:00", true)]
        [InlineData("2020-12-31 23:59:00", "2021-01-01 00:00:00", false)]
        public void CompareGreater(string dateTime1, string dateTime2, bool expected)
        {
            // arrange
            var clock1 = new Clock(DateTime.ParseExact(dateTime1, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            var clock2 = new Clock(DateTime.ParseExact(dateTime2, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));

            // act
            Assert.Equal(expected, clock1 > clock2);
        }

        /// <summary>
        /// Test the less or equal operator of the clock.
        /// </summary>
        [Theory]
        [InlineData("2020-12-31 23:59:00", "2020-12-31 23:59:00", true)]
        [InlineData("2021-01-01 00:00:00", "2020-12-31 23:59:00", false)]
        [InlineData("2020-12-31 23:59:00", "2021-01-01 00:00:00", true)]
        public void CompareLessOrEqual(string dateTime1, string dateTime2, bool expected)
        {
            // arrange
            var clock1 = new Clock(DateTime.ParseExact(dateTime1, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            var clock2 = new Clock(DateTime.ParseExact(dateTime2, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));

            // act
            Assert.Equal(expected, clock1 <= clock2);
        }

        /// <summary>
        /// Test the greater or equals operator of the clock.
        /// </summary>
        [Theory]
        [InlineData("2020-12-31 23:59:00", "2020-12-31 23:59:00", true)]
        [InlineData("2021-01-01 00:00:00", "2020-12-31 23:59:00", true)]
        [InlineData("2020-12-31 23:59:00", "2021-01-01 00:00:00", false)]
        public void CompareGreaterOrEqual(string dateTime1, string dateTime2, bool expected)
        {
            // arrange
            var clock1 = new Clock(DateTime.ParseExact(dateTime1, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            var clock2 = new Clock(DateTime.ParseExact(dateTime2, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));

            // act
            Assert.Equal(expected, clock1 >= clock2);
        }

        /// <summary>
        /// Test the carry of the clock.
        /// </summary>
        [Theory]
        [InlineData("2020-12-31 23:59:00", "2021-01-01 00:00:00")]
        [InlineData("2021-02-27 23:58:00", "2021-02-27 23:59:00")]
        [InlineData("2021-02-28 23:59:00", "2021-03-01 00:00:00")]
        [InlineData("2024-02-28 23:59:00", "2024-02-29 00:00:00")]
        [InlineData("2024-02-29 23:59:00", "2024-03-01 00:00:00")]
        public void Tick(string dateTime1, string expected)
        {
            var clock1 = new Clock(DateTime.ParseExact(dateTime1, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            var clock2 = new Clock(DateTime.ParseExact(expected, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            clock1.Tick();

            // act
            Assert.Equal(clock2, clock1);
        }
    }
}
