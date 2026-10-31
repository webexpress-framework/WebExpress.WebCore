using WebExpress.WebCore.Test.Fixture;
using WebExpress.WebCore.WebJob;
using WebExpress.WebCore.WebLog;

namespace WebExpress.WebCore.Test.Schedule
{
    /// <summary>
    /// Test the cron job of the scheduler.
    /// </summary>
    public class UnitTestCron
    {
        /// <summary>
        /// Verifies that the cron‑matching logic works correctly for a given schedule.
        /// </summary>
        [Fact]
        public void Create_1()
        {
            // arrange
            var context = UnitTestFixture.CreateHttpServerContextMock();
            var clock = new Clock();
            var cron = new Cron(context, "0-59", "*", "1-31", "1-2,3,4,5,6,7,8-10,11,12");

            // act
            Assert.True(cron.Matching(clock));
        }

        /// <summary>
        /// Verifies that the cron‑matching logic works correctly for a given schedule.
        /// </summary>
        [Fact]
        public void Create_2()
        {
            // arrange
            var context = UnitTestFixture.CreateHttpServerContextMock();
            var dateTime = DateTime.Now;
            var clock = new Clock(new DateTime(dateTime.Year, 1, dateTime.Day, dateTime.Hour, dateTime.Minute, 0));
            var cron = new Cron(context, "*", "*", "0-33", "2, 1-4, x");

            // act
            Assert.True(cron.Matching(clock));
        }

        /// <summary>
        /// Verifies that the cron‑matching logic works correctly for a given schedule.
        /// </summary>
        [Fact]
        public void Create_3()
        {
            // arrange
            var context = UnitTestFixture.CreateHttpServerContextMock();
            var dateTime = DateTime.Now;
            var clock = new Clock(new DateTime(dateTime.Year, 12, 31, dateTime.Hour, dateTime.Minute, 0));
            var cron = new Cron(context, "*", "*", "31", "12");

            // act
            Assert.True(cron.Matching(clock));
        }

        /// <summary>
        /// Verifies that the cron‑matching logic works correctly for a given schedule.
        /// </summary>
        [Fact]
        public void Create_4()
        {
            // arrange
            var context = UnitTestFixture.CreateHttpServerContextMock();
            var dateTime = DateTime.Now;
            Log.Current.Clear();

            var clock = new Clock(new DateTime(dateTime.Year, 12, 31, dateTime.Hour, dateTime.Minute, 0));
            var cron = new Cron(context, "*", "*", "*", "a");

            // act
            Assert.Equal(1, context.Log?.WarningCount);
        }

        /// <summary>
        /// Verifies that the cron‑matching logic works correctly for a given schedule.
        /// </summary>
        [Fact]
        public void Create_5()
        {
            // arrange
            var context = UnitTestFixture.CreateHttpServerContextMock();
            var dateTime = DateTime.Now;
            var clock = new Clock(new DateTime(dateTime.Year, 12, 31, dateTime.Hour, dateTime.Minute, 0));
            var cron = new Cron(context, "*", "*", "*", "");

            // act
            Assert.True(cron.Matching(clock));
        }

        /// <summary>
        /// Verifies that the cron‑matching logic works correctly for a given schedule.
        /// </summary>
        [Fact]
        public void Create_6()
        {
            // arrange
            var context = UnitTestFixture.CreateHttpServerContextMock();
            var dateTime = DateTime.Now;
            Log.Current.Clear();

            var clock = new Clock(new DateTime(dateTime.Year, 12, 31, dateTime.Hour, dateTime.Minute, 0));
            var cron = new Cron(context, "99", "*", "*", "*");

            // act
            Assert.Equal(1, context.Log?.WarningCount);
        }

        /// <summary>
        /// Verifies that the cron expression does not match the specified clock time for the given test scenario.
        /// </summary>
        [Fact]
        public void Matching_1()
        {
            // arrange
            var context = UnitTestFixture.CreateHttpServerContextMock();
            var dateTime = DateTime.Now;
            var clock = new Clock(new DateTime(dateTime.Year, 12, 31, dateTime.Hour, dateTime.Minute, 0));
            var cron = new Cron(context, "*", "*", "31", "1-11");

            // act
            Assert.False(cron.Matching(clock));
        }

        /// <summary>
        /// Verifies that the cron expression does not match the specified clock time for the given test scenario.
        /// </summary>
        [Fact]
        public void Matching_2()
        {
            // arrange
            var context = UnitTestFixture.CreateHttpServerContextMock();
            var dateTime = DateTime.Now;
            var clock = new Clock(new DateTime(2020, 1, 1, dateTime.Hour, dateTime.Minute, 0)); // wednesday
            var cron = new Cron(context, "*", "*", "*", "*", "3"); // wednesday

            // act
            Assert.True(cron.Matching(clock));
        }

        /// <summary>
        /// Verifies that the cron expression does not match the specified clock time for the given test scenario.
        /// </summary>
        [Fact]
        public void Matching_3()
        {
            // arrange
            var context = UnitTestFixture.CreateHttpServerContextMock();
            var dateTime = DateTime.Now;
            var clock = new Clock(new DateTime(2020, 1, 1, dateTime.Hour, dateTime.Minute, 0)); // wednesday
            var cron = new Cron(context, "*", "*", "*", "*", "1"); // sunday

            // act
            Assert.False(cron.Matching(clock));
        }
    }
}
