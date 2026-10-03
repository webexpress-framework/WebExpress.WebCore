using System.Reflection;
using WebExpress.WebCore.Test.Fixture;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebCore.WebJob;
using WebExpress.WebCore.WebJob.Model;

namespace WebExpress.WebCore.Test.Manager
{
    /// <summary>
    /// Test the job manager.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestJobManager
    {
        /// <summary>
        /// Test the register function of the job manager.
        /// </summary>
        [Fact]
        public void Register()
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();

            // act
            Assert.Equal(3, componentHub.JobManager.Jobs.Count());
        }

        /// <summary>
        /// Test the remove function of the job manager.
        /// </summary>
        [Fact]
        public void Remove()
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();
            var plugin = componentHub.PluginManager?.GetPlugin(typeof(TestPlugin));
            var jobManager = componentHub.JobManager as JobManager;

            // act
            jobManager.Remove(plugin);

            Assert.Empty(componentHub.JobManager.Jobs);
        }

        /// <summary>
        /// Removing the jobs of a plugin releases them at once rather than when the manager shuts
        /// down, so an unloaded plugin leaves no job instance behind.
        /// </summary>
        [Fact]
        public void RemoveDisposesJobsImmediately()
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();
            var plugin = componentHub.PluginManager?.GetPlugin(typeof(TestPlugin));
            var jobManager = componentHub.JobManager as JobManager;
            var scheduleItems = GetScheduleItems(jobManager);

            // act
            jobManager.Remove(plugin);

            // validation
            Assert.NotEmpty(scheduleItems);
            Assert.All(scheduleItems, x =>
            {
                Assert.True(x.IsDisposed);
                Assert.True(((TestJobA)x.Instance).IsDisposed);
                Assert.True(x.TokenSource.IsCancellationRequested);
            });
        }

        /// <summary>
        /// Removing an application releases the jobs bound to it and leaves those of the other
        /// applications running.
        /// </summary>
        [Fact]
        public void RemoveApplicationDisposesOnlyItsJobs()
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();
            var application = componentHub.ApplicationManager.GetApplications(typeof(TestApplicationA)).First();
            var jobManager = componentHub.JobManager as JobManager;
            var scheduleItems = GetScheduleItems(jobManager);

            // act
            jobManager.Remove(application);

            // validation
            Assert.All(scheduleItems, x => Assert.Equal(x.ApplicationContext == application, x.IsDisposed));
            Assert.Equal(scheduleItems.Count - 1, componentHub.JobManager.Jobs.Count());
        }

        /// <summary>
        /// Returns the schedule entries of all static jobs currently registered.
        /// </summary>
        /// <param name="jobManager">The job manager.</param>
        /// <returns>The schedule entries.</returns>
        private static List<ScheduleItem> GetScheduleItems(JobManager jobManager)
        {
            var dictionary = typeof(JobManager)
                .GetField("_staticScheduleDictionary", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(jobManager) as ScheduleDictionary;

            return [.. dictionary.Values
                .SelectMany(x => x.Values)
                .SelectMany(x => x.Values)
                .SelectMany(x => x)];
        }

        /// <summary>
        /// Tests whether the job manager implements interface IComponentManager.
        /// </summary>
        [Fact]
        public void IsIComponentManager()
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();

            // act
            Assert.True(typeof(IComponentManager).IsAssignableFrom(componentHub.ResourceManager.GetType()));
        }

        /// <summary>
        /// Tests whether the job context implements interface IContext.
        /// </summary>
        [Fact]
        public void IsIContext()
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();

            // act
            foreach (var job in componentHub.JobManager.Jobs)
            {
                Assert.True(typeof(IContext).IsAssignableFrom(job.GetType()), $"Job context {job.GetType().Name} does not implement IContext.");
            }
        }

        /// <summary>
        /// Test the id property of the job.
        /// </summary>
        [Theory]
        [InlineData(typeof(TestApplicationA), typeof(TestJobA), "webexpress.webcore.test.testjoba")]
        [InlineData(typeof(TestApplicationB), typeof(TestJobA), "webexpress.webcore.test.testjoba")]
        [InlineData(typeof(TestApplicationC), typeof(TestJobA), "webexpress.webcore.test.testjoba")]

        public void Id(Type applicationType, Type jobType, string id)
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();
            var application = componentHub.ApplicationManager.GetApplications(applicationType).FirstOrDefault();
            var job = componentHub.JobManager.GetJob(application, jobType);

            // act
            Assert.Equal(id, job?.JobId.ToString());
        }

        /// <summary>
        /// Test the cron property of the job.
        /// </summary>
        [Theory]
        [InlineData(typeof(TestApplicationA), typeof(TestJobA), 50, 8, 31, new[] { 1, 2 }, 0)]
        [InlineData(typeof(TestApplicationB), typeof(TestJobA), 50, 8, 31, new[] { 1, 2 }, 0)]
        [InlineData(typeof(TestApplicationC), typeof(TestJobA), 50, 8, 31, new[] { 1, 2 }, 0)]
        public void Cron(Type applicationType, Type jobType, int minute, int hour, int day, int[] month, int weekday)
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();
            var application = componentHub.ApplicationManager.GetApplications(applicationType).FirstOrDefault();
            var job = componentHub.JobManager.GetJob(application, jobType);

            // act
            Assert.Equal(minute, job?.Cron.Minute.FirstOrDefault() ?? -1);
            Assert.Equal(hour, job?.Cron.Hour.FirstOrDefault() ?? -1);
            Assert.Equal(day, job?.Cron.Day.FirstOrDefault() ?? -1);
            Assert.True(month.SequenceEqual(job?.Cron.Month ?? []));
            Assert.Equal(weekday, job?.Cron.Weekday.FirstOrDefault() ?? -1);
        }
    }
}
