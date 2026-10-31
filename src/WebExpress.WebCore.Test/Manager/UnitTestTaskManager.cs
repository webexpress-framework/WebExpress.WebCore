using WebExpress.WebCore.Test.Fixture;
using WebExpress.WebCore.WebComponent;

namespace WebExpress.WebCore.Test.Manager
{
    /// <summary>
    /// Test the task manager.
    /// </summary>
    public class UnitTestTaskManager
    {
        /// <summary>
        /// Tests whether the task manager implements interface IComponentManager.
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
        /// Tests whether the task context implements interface IComponent.
        /// </summary>
        [Fact]
        public void IsCompopnent()
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();

            componentHub.TaskManager.CreateTask("test");

            // act
            foreach (var task in componentHub.TaskManager.Tasks)
            {
                Assert.True(typeof(IComponent).IsAssignableFrom(task.GetType()), $"Task {task.GetType().Name} does not implement IComponent.");
            }
        }

        /// <summary>
        /// Test the create task of the task manager.
        /// </summary>
        [Fact]
        public void CreateSystemTask()
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();

            // act
            var task = componentHub.TaskManager.CreateTask("test");
            Assert.Equal("test", task?.Id);
        }

        /// <summary>
        /// Test the create task of the task manager.
        /// </summary>
        [Fact]
        public void CreateOwnTask()
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();

            // act
            var task = componentHub.TaskManager.CreateTask<TestTask>("test", null, []);
            Assert.Equal("test", task?.Id);
        }

        /// <summary>
        /// Test the contains task function of the task manager.
        /// </summary>
        [Fact]
        public void ContainsTask()
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();
            var task = componentHub.TaskManager.CreateTask("test");

            // act
            var res = componentHub.TaskManager.ContainsTask("test");
            Assert.True(res);
        }

        /// <summary>
        /// Test the get task function of the task manager.
        /// </summary>
        [Fact]
        public void GetTask()
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();
            var task = componentHub.TaskManager.CreateTask("test");

            // act
            var res = componentHub.TaskManager.GetTask("test");
            Assert.Equal(task, res);
        }

        /// <summary>
        /// Test the remove task function of the task manager.
        /// </summary>
        [Fact]
        public void RemoveTask()
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();
            var task = componentHub.TaskManager.CreateTask("test");

            // act
            componentHub.TaskManager.RemoveTask(task);
            Assert.Empty(componentHub.TaskManager.Tasks);
        }
    }
}
