using WebExpress.WebCore.Test.Fixture;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebCore.WebInclude;

namespace WebExpress.WebCore.Test.Manager
{
    /// <summary>
    /// Test the include manager.
    /// </summary>
    public class UnitTestIncludeManager
    {
        /// <summary>
        /// Test the register function of the include manager.
        /// </summary>
        [Fact]
        public void Register()
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();

            // act
            Assert.Equal(12, componentHub.IncludeManager.Includes.Count());
        }

        /// <summary>
        /// Test the remove function of the include manager.
        /// </summary>
        [Fact]
        public void Remove()
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();
            var plugin = componentHub.PluginManager?.GetPlugin(typeof(TestPlugin));
            var includeManager = componentHub.IncludeManager as IncludeManager;

            // act
            includeManager.Remove(plugin);

            // validation
            Assert.Empty(componentHub.IncludeManager.Includes);
        }

        /// <summary>
        /// Test the id property of the include.
        /// </summary>
        [Theory]
        [InlineData(typeof(TestApplicationA), typeof(TestIncludeJavaScriptA), "webexpress.webcore.test.testincludejavascripta")]
        [InlineData(typeof(TestApplicationA), typeof(TestIncludeJavaScriptB), "webexpress.webcore.test.testincludejavascriptb")]
        [InlineData(typeof(TestApplicationA), typeof(TestIncludeCssA), "webexpress.webcore.test.testincludecssa")]
        [InlineData(typeof(TestApplicationA), typeof(TestIncludeCssB), "webexpress.webcore.test.testincludecssb")]
        [InlineData(typeof(TestApplicationB), typeof(TestIncludeJavaScriptA), "webexpress.webcore.test.testincludejavascripta")]
        [InlineData(typeof(TestApplicationB), typeof(TestIncludeJavaScriptB), "webexpress.webcore.test.testincludejavascriptb")]
        [InlineData(typeof(TestApplicationB), typeof(TestIncludeCssA), "webexpress.webcore.test.testincludecssa")]
        [InlineData(typeof(TestApplicationB), typeof(TestIncludeCssB), "webexpress.webcore.test.testincludecssb")]
        [InlineData(typeof(TestApplicationC), typeof(TestIncludeJavaScriptA), "webexpress.webcore.test.testincludejavascripta")]
        [InlineData(typeof(TestApplicationC), typeof(TestIncludeJavaScriptB), "webexpress.webcore.test.testincludejavascriptb")]
        [InlineData(typeof(TestApplicationC), typeof(TestIncludeCssA), "webexpress.webcore.test.testincludecssa")]
        [InlineData(typeof(TestApplicationC), typeof(TestIncludeCssB), "webexpress.webcore.test.testincludecssb")]
        public void Id(Type applicationType, Type includeType, string expected)
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();
            var application = componentHub.ApplicationManager.GetApplications(applicationType)?.FirstOrDefault();
            var include = componentHub.IncludeManager.GetIncludes(application, includeType)?.FirstOrDefault();

            // act
            var id = include?.IncludeId.ToString();

            // validation
            AssertExtensions.EqualWithPlaceholders(expected, id);
        }

        /// <summary>
        /// Test the files property of the include.
        /// </summary>
        [Theory]
        [InlineData(typeof(TestApplicationA), typeof(TestIncludeJavaScriptA), "/myA.js;/myB.js;/myC.js")]
        [InlineData(typeof(TestApplicationA), typeof(TestIncludeJavaScriptB), "/myX.js;/myY.js;/myZ.js")]
        [InlineData(typeof(TestApplicationA), typeof(TestIncludeCssA), "/myA.css;/myB.css;/myC.css")]
        [InlineData(typeof(TestApplicationA), typeof(TestIncludeCssB), "/myX.css;/myY.css;/myZ.css")]
        [InlineData(typeof(TestApplicationB), typeof(TestIncludeJavaScriptA), "/myA.js;/myB.js;/myC.js")]
        [InlineData(typeof(TestApplicationB), typeof(TestIncludeJavaScriptB), "/myX.js;/myY.js;/myZ.js")]
        [InlineData(typeof(TestApplicationB), typeof(TestIncludeCssA), "/myA.css;/myB.css;/myC.css")]
        [InlineData(typeof(TestApplicationB), typeof(TestIncludeCssB), "/myX.css;/myY.css;/myZ.css")]
        [InlineData(typeof(TestApplicationC), typeof(TestIncludeJavaScriptA), "/myA.js;/myB.js;/myC.js")]
        [InlineData(typeof(TestApplicationC), typeof(TestIncludeJavaScriptB), "/myX.js;/myY.js;/myZ.js")]
        [InlineData(typeof(TestApplicationC), typeof(TestIncludeCssA), "/myA.css;/myB.css;/myC.css")]
        [InlineData(typeof(TestApplicationC), typeof(TestIncludeCssB), "/myX.css;/myY.css;/myZ.css")]
        public void Files(Type applicationType, Type resourceType, string expected)
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();
            var application = componentHub.ApplicationManager.GetApplications(applicationType)?.FirstOrDefault();
            var include = componentHub.IncludeManager.GetIncludes(application, resourceType)?.FirstOrDefault();

            // act
            var files = include.Files.Select(x => x.FileName);

            // validation
            AssertExtensions.EqualWithPlaceholders(expected, string.Join(";", files));
        }

        /// <summary>
        /// Test the files property of the include.
        /// </summary>
        [Theory]
        [InlineData(typeof(TestApplicationA), typeof(TestIncludeJavaScriptA), "JavaScript;JavaScript;JavaScript")]
        [InlineData(typeof(TestApplicationA), typeof(TestIncludeJavaScriptB), "JavaScript;JavaScript;JavaScript")]
        [InlineData(typeof(TestApplicationA), typeof(TestIncludeCssA), "StyleSheet;StyleSheet;StyleSheet")]
        [InlineData(typeof(TestApplicationA), typeof(TestIncludeCssB), "StyleSheet;StyleSheet;StyleSheet")]
        [InlineData(typeof(TestApplicationB), typeof(TestIncludeJavaScriptA), "JavaScript;JavaScript;JavaScript")]
        [InlineData(typeof(TestApplicationB), typeof(TestIncludeJavaScriptB), "JavaScript;JavaScript;JavaScript")]
        [InlineData(typeof(TestApplicationB), typeof(TestIncludeCssA), "StyleSheet;StyleSheet;StyleSheet")]
        [InlineData(typeof(TestApplicationB), typeof(TestIncludeCssB), "StyleSheet;StyleSheet;StyleSheet")]
        [InlineData(typeof(TestApplicationC), typeof(TestIncludeJavaScriptA), "JavaScript;JavaScript;JavaScript")]
        [InlineData(typeof(TestApplicationC), typeof(TestIncludeJavaScriptB), "JavaScript;JavaScript;JavaScript")]
        [InlineData(typeof(TestApplicationC), typeof(TestIncludeCssA), "StyleSheet;StyleSheet;StyleSheet")]
        [InlineData(typeof(TestApplicationC), typeof(TestIncludeCssB), "StyleSheet;StyleSheet;StyleSheet")]
        public void FileType(Type applicationType, Type resourceType, string expected)
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();
            var application = componentHub.ApplicationManager.GetApplications(applicationType)?.FirstOrDefault();
            var include = componentHub.IncludeManager.GetIncludes(application, resourceType)?.FirstOrDefault();

            // act
            var files = include.Files.Select(x => x.Type);

            // validation
            AssertExtensions.EqualWithPlaceholders(expected, string.Join(";", files));
        }

        /// <summary>
        /// Tests whether the include manager implements interface IComponentManager.
        /// </summary>
        [Fact]
        public void IsIComponentManager()
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();

            // act
            Assert.True(typeof(IComponentManager).IsAssignableFrom(componentHub.IncludeManager.GetType()));
        }

        /// <summary>
        /// Tests whether the include context implements interface IContext.
        /// </summary>
        [Fact]
        public void IsIContext()
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();

            // act
            foreach (var include in componentHub.IncludeManager.Includes)
            {
                Assert.True(typeof(IContext).IsAssignableFrom(include.GetType()), $"Include context '{include.GetType().Name}' does not implement IContext.");
            }
        }
    }
}
