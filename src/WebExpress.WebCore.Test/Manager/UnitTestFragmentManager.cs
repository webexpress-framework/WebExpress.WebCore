using WebExpress.WebCore.Test.Fixture;
using WebExpress.WebCore.Test.WWW;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebPage;
using WebExpress.WebCore.WebScope;

namespace WebExpress.WebCore.Test.Manager
{
    /// <summary>
    /// Test the fragment manager.
    /// </summary>
    public class UnitTestFragmentManager
    {
        /// <summary>
        /// Test the register function of the fragment manager.
        /// </summary>
        [Fact]
        public void Register()
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();

            // act
            Assert.Equal(15, componentHub.FragmentManager.Fragments.Count());
        }

        /// <summary>
        /// Test the remove function of the fragment manager.
        /// </summary>
        [Fact]
        public void Remove()
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();
            var fragmentManager = componentHub.FragmentManager as FragmentManager;
            var plugin = componentHub.PluginManager?.GetPlugin(typeof(TestPlugin));

            // act
            fragmentManager.Remove(plugin);

            // validation
            Assert.Empty(componentHub.FragmentManager.Fragments);
        }

        /// <summary>
        /// Tests whether the fragment manager implements interface IComponentManager.
        /// </summary>
        [Fact]
        public void IsIComponentManager()
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();

            // act
            Assert.True(typeof(IComponentManager).IsAssignableFrom(componentHub.EventManager.GetType()));
        }

        /// <summary>
        /// Test the id property of the fragment.
        /// </summary>
        [Theory]
        [InlineData(typeof(TestApplicationA), typeof(TestFragmentA), "webexpress.webcore.test.testfragmenta")]
        [InlineData(typeof(TestApplicationB), typeof(TestFragmentA), "webexpress.webcore.test.testfragmenta")]
        [InlineData(typeof(TestApplicationC), typeof(TestFragmentA), "webexpress.webcore.test.testfragmenta")]
        [InlineData(typeof(TestApplicationA), typeof(TestFragmentB), "webexpress.webcore.test.testfragmentb")]
        [InlineData(typeof(TestApplicationB), typeof(TestFragmentB), "webexpress.webcore.test.testfragmentb")]
        [InlineData(typeof(TestApplicationC), typeof(TestFragmentB), "webexpress.webcore.test.testfragmentb")]
        [InlineData(typeof(TestApplicationA), typeof(TestFragmentC), "webexpress.webcore.test.testfragmentc")]
        [InlineData(typeof(TestApplicationB), typeof(TestFragmentC), "webexpress.webcore.test.testfragmentc")]
        [InlineData(typeof(TestApplicationC), typeof(TestFragmentC), "webexpress.webcore.test.testfragmentc")]
        public void Id(Type applicationType, Type fragmentType, string id)
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();
            var application = componentHub.ApplicationManager.GetApplications(applicationType).FirstOrDefault();

            // act
            var fragment = componentHub.FragmentManager.GetFragments(application, fragmentType);

            if (id is null)
            {
                Assert.Empty(fragment);
                return;
            }

            // validation
            Assert.Contains(id, fragment.Select(x => x.FragmentId?.ToString()));
        }

        /// <summary>
        /// Test the get fragment function of the fragment.
        /// </summary>
        [Theory]
        [InlineData(typeof(TestApplicationA), typeof(IScope), 0)]
        [InlineData(typeof(TestApplicationA), typeof(TestScopeA), 1)]
        [InlineData(typeof(TestApplicationA), typeof(About), 1)]
        [InlineData(typeof(TestApplicationB), typeof(IScope), 0)]
        [InlineData(typeof(TestApplicationB), typeof(About), 1)]
        public void GetFragments(Type applicationType, Type scopeType, int count)
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();
            var application = componentHub.ApplicationManager.GetApplications(applicationType).FirstOrDefault();
            var renderContext = UnitTestFixture.CrerateRenderContextMock(application, [scopeType]);

            // act
            var fragments = componentHub.FragmentManager.GetFragments<TestFragmentA, TestSectionA>(application, renderContext?.PageContext?.Scopes).ToList();

            // validation
            Assert.NotNull(fragments);
            Assert.Equal(count, fragments.Count);
        }

        /// <summary>
        /// Test the get fragment function of the fragment.
        /// </summary>
        [Theory]
        [InlineData(typeof(TestApplicationA), typeof(IScope), 0)]
        [InlineData(typeof(TestApplicationA), typeof(TestScopeA), 1)]
        [InlineData(typeof(TestApplicationA), typeof(About), 1)]
        [InlineData(typeof(TestApplicationB), typeof(IScope), 0)]
        [InlineData(typeof(TestApplicationB), typeof(About), 1)]
        public void GetFragmentsBase(Type applicationType, Type scopeType, int count)
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();
            var application = componentHub.ApplicationManager.GetApplications(applicationType).FirstOrDefault();
            var renderContext = UnitTestFixture.CrerateRenderContextMock(application, [scopeType]);

            // act
            var fragments = componentHub.FragmentManager.GetFragments<IFragment, TestSectionA>(application, renderContext?.PageContext?.Scopes).ToList();

            // validation
            Assert.NotNull(fragments);
            Assert.Equal(count, fragments.Count);
        }

        /// <summary>
        /// Test the Render function of the fragment.
        /// </summary>
        [Theory]
        [InlineData(typeof(TestApplicationA), typeof(TestSectionA), typeof(TestScopeA), false)]
        [InlineData(typeof(TestApplicationA), typeof(TestSectionA), typeof(TestScopeB), false)]
        [InlineData(typeof(TestApplicationA), typeof(TestSectionA), typeof(About), false)]
        [InlineData(typeof(TestApplicationA), typeof(TestSectionA), typeof(IScope), true)]
        [InlineData(typeof(TestApplicationA), typeof(TestSectionA), typeof(TestScopeD), true)]
        public void Render(Type applicationType, Type sectionType, Type scopeType, bool empty)
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();
            var application = componentHub.ApplicationManager.GetApplications(applicationType).FirstOrDefault();
            var renderContext = UnitTestFixture.CrerateRenderContextMock(application, [scopeType]);
            var visualTree = new VisualTree();

            // act
            var html = componentHub.FragmentManager.Render<IRenderContext, IVisualTree>(renderContext, visualTree, sectionType);

            // validation
            Assert.NotNull(html);

            if (!empty)
            {
                Assert.NotEmpty(html.FirstOrDefault()?.ToString());
            }
            else
            {
                Assert.Null(html.FirstOrDefault());
            }
        }
    }
}
