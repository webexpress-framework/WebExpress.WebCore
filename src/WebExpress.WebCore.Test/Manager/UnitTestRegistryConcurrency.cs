using WebExpress.WebCore.Test.Fixture;
using WebExpress.WebCore.WebInclude;
using WebExpress.WebCore.WebPage;

namespace WebExpress.WebCore.Test.Manager
{
    /// <summary>
    /// Tests that the registries announce what they remove and stay consistent when requests use
    /// them while they change.
    /// </summary>
    public class UnitTestRegistryConcurrency
    {
        /// <summary>
        /// Verifies that removing a plugin announces each of its pages. The removed pages were read
        /// after they had left the registry, so no page was disposed and no endpoint was told that
        /// its page was gone.
        /// </summary>
        [Fact]
        public void RemovingAPluginAnnouncesItsPages()
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();
            var plugin = componentHub.PluginManager.GetPlugin(typeof(TestPlugin));
            var pageManager = componentHub.PageManager as PageManager;
            var registered = componentHub.PageManager.Pages.Count();
            var removed = new List<IPageContext>();
            pageManager.RemovePage += (sender, e) => removed.Add(e);

            // act
            pageManager.Remove(plugin);

            // validation
            Assert.True(registered > 0);
            Assert.Equal(registered, removed.Count);
            Assert.Empty(componentHub.PageManager.Pages);
        }

        /// <summary>
        /// Verifies that removing a plugin announces each of its includes and leaves none behind.
        /// </summary>
        [Fact]
        public void RemovingAPluginAnnouncesItsIncludes()
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();
            var plugin = componentHub.PluginManager.GetPlugin(typeof(TestPlugin));
            var includeManager = componentHub.IncludeManager as IncludeManager;
            var registered = componentHub.IncludeManager.Includes.Count();
            var removed = new List<IIncludeContext>();
            includeManager.RemoveInclude += (sender, e) => removed.Add(e);

            // act
            includeManager.Remove(plugin);

            // validation
            Assert.Equal(registered, removed.Count);
            Assert.Empty(componentHub.IncludeManager.Includes);
        }

        /// <summary>
        /// Verifies that requests creating the same task at the same time get one task. The plain
        /// dictionary threw on the second insert of an id, or was corrupted by concurrent writes.
        /// </summary>
        [Fact]
        public void ConcurrentTaskCreationYieldsOneTask()
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();
            var taskManager = componentHub.TaskManager;

            // act
            var tasks = Enumerable.Range(0, 64)
                .AsParallel()
                .WithDegreeOfParallelism(16)
                .Select(i => taskManager.CreateTask("Concurrent-" + (i % 4)))
                .ToList();

            // validation
            Assert.Equal(4, tasks.Distinct().Count());
            Assert.Same(taskManager.GetTask("concurrent-1"), taskManager.GetTask("CONCURRENT-1"));
        }

        /// <summary>
        /// Verifies that reading the page registry while a plugin is removed and registered again
        /// never throws.
        /// </summary>
        [Fact]
        public void ReadingPagesWhilePluginsChange()
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();
            var plugin = componentHub.PluginManager.GetPlugin(typeof(TestPlugin));
            var pageManager = componentHub.PageManager as PageManager;
            using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            var register = typeof(PageManager).GetMethod("Register", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance, [typeof(WebExpress.WebCore.WebPlugin.IPluginContext)]);

            // act
            var reader = Task.Run(() =>
            {
                while (!stop.IsCancellationRequested)
                {
                    _ = componentHub.PageManager.Pages.Count();
                }
            });

            for (var i = 0; i < 50; i++)
            {
                pageManager.Remove(plugin);
                register.Invoke(pageManager, [plugin]);
            }

            stop.Cancel();

            // validation
            reader.Wait();
            Assert.True(reader.IsCompletedSuccessfully);
        }
    }
}
