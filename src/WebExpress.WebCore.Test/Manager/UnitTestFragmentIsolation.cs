using WebExpress.WebCore.Test.Fixture;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebFragment.Model;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebCore.WebPage;
using WebExpress.WebCore.WebScope;

namespace WebExpress.WebCore.Test.Manager
{
    /// <summary>
    /// Tests that fragments are rendered on their own instance and leave the registry completely
    /// when their plugin goes.
    /// </summary>
    public class UnitTestFragmentIsolation
    {
        /// <summary>
        /// A fragment that renders the number of its instance. It is nested, so the fragment scan,
        /// which only takes public top-level classes, does not register it.
        /// </summary>
        public sealed class InstanceFragment : IFragment<IRenderContext, IVisualTree>
        {
            private static int _next;
            private readonly int _number = Interlocked.Increment(ref _next);

            /// <summary>
            /// Gets the id.
            /// </summary>
            public string Id => string.Empty;

            /// <summary>
            /// Renders the number of the instance.
            /// </summary>
            /// <param name="renderContext">The render context.</param>
            /// <param name="visualTree">The visual tree.</param>
            /// <returns>The number as text.</returns>
            public IHtmlNode Render(IRenderContext renderContext, IVisualTree visualTree)
            {
                return new HtmlText($"instance {_number}");
            }

            /// <summary>
            /// Releases nothing.
            /// </summary>
            public void Dispose()
            {
            }
        }

        /// <summary>
        /// Verifies that every render of an uncached fragment runs on a fresh instance. The compiled
        /// render call captured the instance of the first request, so every later request rendered
        /// on that one - its state leaked to other requests and users.
        /// </summary>
        [Fact]
        public void UncachedFragmentRendersOnItsOwnInstance()
        {
            // arrange
            var item = CreateItem(cache: false, out var renderContext);

            // act
            var first = (item.Render<IRenderContext, IVisualTree>(renderContext, new VisualTree()) as HtmlText)?.Value;
            var second = (item.Render<IRenderContext, IVisualTree>(renderContext, new VisualTree()) as HtmlText)?.Value;

            // validation
            Assert.NotNull(first);
            Assert.NotEqual(first, second);
        }

        /// <summary>
        /// Verifies that a cached fragment keeps rendering on the one instance it keeps.
        /// </summary>
        [Fact]
        public void CachedFragmentRendersOnTheKeptInstance()
        {
            // arrange
            var item = CreateItem(cache: true, out var renderContext);

            // act
            var first = (item.Render<IRenderContext, IVisualTree>(renderContext, new VisualTree()) as HtmlText)?.Value;
            var second = (item.Render<IRenderContext, IVisualTree>(renderContext, new VisualTree()) as HtmlText)?.Value;

            // validation
            Assert.Equal(first, second);
        }

        /// <summary>
        /// Verifies that removing a plugin announces each of its fragments; the removed fragments
        /// were read after they had left the registry, so no remove event ever fired.
        /// </summary>
        [Fact]
        public void RemovingAPluginAnnouncesItsFragments()
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();
            var fragmentManager = componentHub.FragmentManager as FragmentManager;
            var plugin = componentHub.PluginManager.GetPlugin(typeof(TestPlugin));
            var registered = componentHub.FragmentManager.Fragments.Count();
            var removed = new List<IFragmentContext>();
            fragmentManager.RemoveFragment += (sender, e) => removed.Add(e);

            // act
            fragmentManager.Remove(plugin);

            // validation
            Assert.True(registered > 0);
            Assert.Equal(registered, removed.Count);
            Assert.Empty(componentHub.FragmentManager.Fragments);
        }

        /// <summary>
        /// Verifies that removing every application of a plugin empties the registry and announces
        /// each fragment once.
        /// </summary>
        [Fact]
        public void RemovingApplicationsAnnouncesTheirFragments()
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();
            var fragmentManager = componentHub.FragmentManager as FragmentManager;
            var registered = componentHub.FragmentManager.Fragments.Count();
            var removed = new List<IFragmentContext>();
            fragmentManager.RemoveFragment += (sender, e) => removed.Add(e);

            // act
            foreach (var application in componentHub.ApplicationManager.Applications.ToList())
            {
                fragmentManager.Remove(application);
            }

            // validation
            Assert.Equal(registered, removed.Count);
            Assert.Empty(componentHub.FragmentManager.Fragments);
        }

        /// <summary>
        /// Verifies that asking for the fragments of several scopes returns instead of recursing
        /// into itself until the stack overflowed.
        /// </summary>
        [Fact]
        public void FragmentsOfSeveralScopes()
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();
            var application = componentHub.ApplicationManager.GetApplications(typeof(TestApplicationA)).FirstOrDefault();

            // act
            var fragments = componentHub.FragmentManager.GetFragments(application, typeof(TestSectionA), [typeof(TestScopeA), typeof(IScope)]);

            // validation
            Assert.NotEmpty(fragments);
        }

        /// <summary>
        /// Creates a fragment item of the instance fragment, borrowing the context of a registered
        /// fragment whose conditions let it render.
        /// </summary>
        /// <param name="cache">Whether the item keeps one instance.</param>
        /// <param name="renderContext">The render context the fragment renders in.</param>
        /// <returns>The fragment item.</returns>
        private static FragmentItem CreateItem(bool cache, out IRenderContext renderContext)
        {
            var httpServerContext = UnitTestFixture.CreateHttpServerContextMock();
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock(httpServerContext);
            var application = componentHub.ApplicationManager.GetApplications(typeof(TestApplicationA)).FirstOrDefault();
            var fragmentContext = componentHub.FragmentManager.GetFragments(application, typeof(TestFragmentA)).First();

            renderContext = UnitTestFixture.CrerateRenderContextMock(application, [typeof(TestScopeA)]);

            return new FragmentItem(componentHub, httpServerContext)
            {
                FragmentContext = fragmentContext,
                FragmentClass = typeof(InstanceFragment),
                Cache = cache
            };
        }
    }
}
