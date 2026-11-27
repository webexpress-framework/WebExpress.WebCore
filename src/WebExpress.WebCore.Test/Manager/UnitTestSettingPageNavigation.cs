using System.Reflection;
using WebExpress.WebCore.Test.Fixture;
using WebExpress.WebCore.Test.WWW.Settings;
using WebExpress.WebCore.WebApplication;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebCore.WebEndpoint;
using WebExpress.WebCore.WebLog;
using WebExpress.WebCore.WebPlugin;
using WebExpress.WebCore.WebPlugin.Model;
using WebExpress.WebCore.WebSettingPage;

namespace WebExpress.WebCore.Test.Manager
{
    /// <summary>
    /// Tests that the settings navigation - categories, their groups and the pages of each group -
    /// comes out the same whatever order the plugins providing them register in.
    /// </summary>
    /// <remarks>
    /// The plugins of an application register in the order of a hash of their ids, which .NET
    /// seeds anew on every start. A page of one plugin that names a group of another (a framework
    /// group such as the general settings) must therefore find it even when it registers first.
    /// </remarks>
    public class UnitTestSettingPageNavigation
    {
        private static readonly Type[] _categories = [typeof(TestSettingCategoryA), typeof(TestSettingCategoryB), typeof(TestSettingCategoryC)];
        private static readonly Type[] _groups = [typeof(TestSettingGroupA), typeof(TestSettingGroupB), typeof(TestSettingGroupC)];
        private static readonly Type[] _pages = [typeof(TestSettingPageA), typeof(TestSettingPageB), typeof(TestSettingPageC)];

        /// <summary>
        /// Every order of the three plugins - categories, groups, pages - registered as separate
        /// passes and as a single one.
        /// </summary>
        public static TheoryData<int[], bool> Orders()
        {
            var data = new TheoryData<int[], bool>();

            foreach (var order in new[] { new[] { 0, 1, 2 }, [0, 2, 1], [1, 0, 2], [1, 2, 0], [2, 0, 1], [2, 1, 0] })
            {
                data.Add(order, false);
                data.Add(order, true);
            }

            return data;
        }

        /// <summary>
        /// Tests that the navigation is the same in every registration order, pages before
        /// their group and groups before their category included.
        /// </summary>
        [Theory]
        [MemberData(nameof(Orders))]
        public void NavigationDoesNotDependOnTheRegistrationOrder(int[] order, bool singlePass)
        {
            // arrange
            var (manager, _) = CreateManager();
            var owner = Plugin("owner");
            var application = Application(owner);
            var plugins = new (IPluginContext, IEnumerable<Type>)[]
            {
                (Plugin("categories"), _categories),
                (Plugin("groups"), _groups),
                (Plugin("pages"), _pages)
            };

            // act
            if (singlePass)
            {
                manager.Register([.. order.Select(x => plugins[x])], [application]);
            }
            else
            {
                foreach (var index in order)
                {
                    manager.Register([plugins[index]], [application]);
                }
            }

            // validation
            Assert.Equal(ExpectedNavigation, Navigation(manager, application));
        }

        /// <summary>
        /// Tests that the plugin that comes with the application and the one that comes as a
        /// plugin of its own register once, not twice, when both events reach the manager.
        /// </summary>
        [Fact]
        public void PluginRegisteredTwiceIsListedOnce()
        {
            // arrange
            var (manager, _) = CreateManager();
            var plugin = Plugin("groups");
            var application = Application(plugin);

            // act
            manager.Register([(plugin, _categories.Concat(_groups))], [application]);
            manager.Register([(plugin, _categories.Concat(_groups))], [application]);

            // validation
            Assert.Equal(3, manager.GetSettingCategories(application).Count());
            Assert.Equal(3, manager.SettingGroups.Count());
        }

        /// <summary>
        /// Tests that a page whose group is not registered stays registered, is reported with
        /// the page, the group and the assembly of the group, and joins the navigation once the
        /// group arrives.
        /// </summary>
        [Fact]
        public void UnresolvedGroupIsReportedAndResolvedLater()
        {
            // arrange
            var (manager, log) = CreateManager();
            var application = Application(Plugin("owner"));

            // act
            manager.Register([(Plugin("pages"), _pages)], [application]);
            var warnings = Warnings(log);
            var before = Navigation(manager, application);

            manager.Register([(Plugin("framework"), _categories.Concat(_groups))], [application]);

            // validation
            Assert.Equal(3, manager.GetSettingPages(typeof(TestSettingPageA), application).Count() + manager.GetSettingPages(typeof(TestSettingPageB), application).Count() + manager.GetSettingPages(typeof(TestSettingPageC), application).Count());
            var warning = Assert.Single(warnings, x => x.Contains(typeof(TestSettingPageA).FullName));
            Assert.Contains(typeof(TestSettingGroupA).FullName, warning);
            Assert.Contains(typeof(TestSettingGroupA).Assembly.GetName().Name, warning);
            Assert.DoesNotContain(typeof(TestSettingPageA).FullName.ToLowerInvariant(), before);
            Assert.Equal(ExpectedNavigation, Navigation(manager, application));
        }

        /// <summary>
        /// Tests that no page is reported when its group comes from a plugin later in the same
        /// pass - the case of a framework group whose plugin happens to be iterated last.
        /// </summary>
        [Fact]
        public void GroupLaterInThePassIsNotReported()
        {
            // arrange
            var (manager, log) = CreateManager();
            var application = Application(Plugin("owner"));

            // act
            manager.Register([(Plugin("pages"), _pages), (Plugin("framework"), _categories.Concat(_groups))], [application]);

            // validation
            Assert.DoesNotContain(Warnings(log), x => x.Contains(typeof(TestSettingPageA).FullName) || x.Contains(typeof(TestSettingPageB).FullName));
        }

        /// <summary>
        /// Tests that a removed group takes its pages out of the navigation instead of leaving
        /// them under a group that no longer exists.
        /// </summary>
        [Fact]
        public void RemovedGroupTakesItsPagesOutOfTheNavigation()
        {
            // arrange
            var (manager, _) = CreateManager();
            var application = Application(Plugin("owner"));
            var framework = Plugin("framework");
            manager.Register([(framework, _categories.Concat(_groups)), (Plugin("pages"), _pages)], [application]);

            // act
            manager.Remove(framework);

            // validation
            Assert.All(manager.SettingPages, x => Assert.Null(x.SettingGroup));
            Assert.Equal(string.Empty, Navigation(manager, application));
        }

        /// <summary>
        /// Tests that plugins come after the plugins they depend on, whatever order the
        /// dictionary holds them in, and that independent plugins keep one fixed order.
        /// </summary>
        [Theory]
        [InlineData("kleenestar.core", "webexpress.webapp", "webexpress.webui")]
        [InlineData("webexpress.webui", "kleenestar.core", "webexpress.webapp")]
        [InlineData("webexpress.webapp", "webexpress.webui", "kleenestar.core")]
        public void PluginsFollowTheirDependencies(params string[] arrival)
        {
            // arrange
            var dependencies = new Dictionary<string, string[]>
            {
                ["kleenestar.core"] = ["WebExpress.WebApp"],
                ["webexpress.webapp"] = ["WebExpress.WebUI"],
                ["webexpress.webui"] = []
            };
            var items = arrival.Select(x => new PluginItem
            {
                PluginContext = Plugin(x),
                Dependencies = dependencies[x]
            });

            // act
            var ordered = PluginManager.InDependencyOrder(items).Select(x => x.PluginContext.PluginId.ToString());

            // validation
            Assert.Equal(["webexpress.webui", "webexpress.webapp", "kleenestar.core"], ordered);
        }

        /// <summary>
        /// Gets the navigation of the test settings: group a and b in category a, group c and
        /// page c without a category or group.
        /// </summary>
        private static string ExpectedNavigation => string.Join(" | ",
            $"{Id<TestSettingCategoryA>()}: {Id<TestSettingGroupA>()} [{Id<TestSettingPageA>()}], {Id<TestSettingGroupB>()} [{Id<TestSettingPageB>()}]",
            $"{Id<TestSettingCategoryB>()}: ",
            $"{Id<TestSettingCategoryC>()}: ");

        /// <summary>
        /// Renders the navigation of an application as text, sorted, so two navigations
        /// compare by their content.
        /// </summary>
        private static string Navigation(SettingPageManager manager, IApplicationContext application)
        {
            return string.Join(" | ", manager.GetSettingCategories(application)
                .OrderBy(x => x.CategoryId.ToString())
                .Select(category => $"{category.CategoryId}: " + string.Join(", ", manager.GetSettingGroups(application, category)
                    .OrderBy(x => x.GroupId.ToString())
                    .Select(group => $"{group.GroupId} [" + string.Join(", ", manager.GetSettingPages(application, group)
                        .Select(x => x.EndpointId.ToString())
                        .Order()) + "]"))));
        }

        /// <summary>
        /// Creates a manager outside a component hub, so the test alone decides which plugins
        /// register and in what order.
        /// </summary>
        private static (SettingPageManager Manager, Log Log) CreateManager()
        {
            // the bound hub only provides the translations of the log messages
            UnitTestFixture.CreateAndRegisterComponentHubMock();

            var log = new Log { LogMode = LogMode.Off };
            var ctor = typeof(SettingPageManager).GetConstructor
            (
                BindingFlags.NonPublic | BindingFlags.Instance,
                [typeof(IComponentHub), typeof(IHttpServerContext)]
            );
            var manager = (SettingPageManager)ctor.Invoke([null, UnitTestFixture.CreateHttpServerContextMock(log: log)]);

            return (manager, log);
        }

        private static PluginContext Plugin(string id)
        {
            return new PluginContext { PluginId = new ComponentId(id), Assembly = typeof(TestSettingPageA).Assembly };
        }

        private static ApplicationContext Application(IPluginContext owner)
        {
            return new ApplicationContext { ApplicationId = "app", PluginContext = owner, Route = new RouteEndpoint("/server/app") };
        }

        private static List<string> Warnings(Log log)
        {
            return [.. log.GetRecentEntries().Where(x => x.Level == LogLevel.Warning).Select(x => x.Message)];
        }

        private static string Id<T>()
        {
            return typeof(T).FullName.ToLowerInvariant();
        }
    }
}
