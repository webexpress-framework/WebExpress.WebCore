using WebExpress.WebCore.Test.Fixture;
using WebExpress.WebCore.WebApplication;
using WebExpress.WebCore.WebPlugin;

namespace WebExpress.WebCore.Test.Manager
{
    /// <summary>
    /// Verifies that the catalog finds exactly the types of the convention, isolates defective
    /// ones, keeps track of who provides which key, and follows the plugins and applications of a host.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestPluginTypeCatalog
    {
        /// <summary>
        /// Only public, top-level, sealed, closed, assignable types with a parameterless constructor are items.
        /// </summary>
        [Fact]
        public void Collect_FollowsTheConvention()
        {
            // arrange
            using var catalog = new PluginTypeCatalog<ICatalogProbe>(x => x.Key);
            var plugin = new HealthTestPluginContext("plugin", typeof(AlphaProbe), typeof(UnsealedProbe), typeof(NestedProbe),
                typeof(GenericProbe<>), typeof(ConstructorProbe), typeof(ICatalogProbe), typeof(string));

            // act
            catalog.Collect(plugin);

            // validation
            Assert.Equal("alpha", Assert.Single(catalog.Items).Key);
            Assert.Same(plugin, catalog.OwnerOf("alpha"));
        }

        /// <summary>
        /// A factory builds the items, receives the origin, and frees the convention from the constructor.
        /// </summary>
        [Fact]
        public void Factory_BuildsTheItemsFromTheirOrigin()
        {
            // arrange
            var origins = new List<PluginTypeOrigin>();
            using var catalog = new PluginTypeCatalog<ICatalogProbe>(x => x.Key, typeof(ICatalogProbe), origin =>
            {
                origins.Add(origin);
                return new ConstructorProbe("made-" + origin.Type.Name.ToLowerInvariant());
            });
            var plugin = new HealthTestPluginContext("plugin", typeof(AlphaProbe), typeof(ConstructorProbe));

            // act
            catalog.Collect(plugin);

            // validation
            Assert.Equal(["made-alphaprobe", "made-constructorprobe"], catalog.Items.Select(x => x.Key).Order());
            Assert.All(origins, x => Assert.Same(plugin, x.PluginContext));
            Assert.All(origins, x => Assert.Null(x.ApplicationContext));
        }

        /// <summary>
        /// A constructor that throws, an item without a key and a second item under a key of the
        /// same plugin are logged and skipped; the plugin's other items are still offered.
        /// </summary>
        [Fact]
        public void Collect_LogsDefectiveTypesAndKeepsTheOthers()
        {
            // arrange
            var context = UnitTestFixture.CreateHttpServerContextMock();
            var hub = UnitTestFixture.CreateComponentHubMock(context);
            using var catalog = new PluginTypeCatalog<ICatalogProbe>(x => x.Key);
            catalog.Attach(hub, context);
            var plugin = new HealthTestPluginContext("defective", typeof(ThrowingProbe), typeof(KeylessProbe), typeof(AlphaProbe), typeof(AlphaTwinProbe), typeof(BetaProbe));

            // act
            catalog.Collect(plugin);

            // validation
            Assert.Equal(["alpha", "beta"], catalog.Items.Select(x => x.Key).Order());
            Assert.IsType<AlphaProbe>(catalog.Find("alpha"));
            var messages = context.Log.GetRecentEntries().Select(x => x.Message).ToArray();
            Assert.Contains(messages, x => x.Contains(typeof(ThrowingProbe).FullName) && x.Contains("probe constructor failed"));
            Assert.Contains(messages, x => x.Contains(typeof(KeylessProbe).FullName));
            Assert.Contains(messages, x => x.Contains(typeof(AlphaTwinProbe).FullName));
        }

        /// <summary>
        /// Keys are trimmed and compared without case.
        /// </summary>
        [Fact]
        public void Keys_AreTrimmedAndCaseInsensitive()
        {
            // arrange
            using var catalog = new PluginTypeCatalog<ICatalogProbe>(x => x.Key);

            // act
            catalog.Collect(new HealthTestPluginContext("plugin", typeof(AlphaProbe)));

            // validation
            Assert.NotNull(catalog.Find(" ALPHA "));
            Assert.True(catalog.Contains("Alpha"));
            Assert.Null(catalog.Find(" "));
            Assert.Null(catalog.Find(null));
        }

        /// <summary>
        /// The latest offer wins; a plugin that leaves takes only what it still provides, and a key
        /// falls back to the offer below it before it leaves.
        /// </summary>
        [Fact]
        public void Ownership_FollowsTheOffers()
        {
            // arrange
            using var catalog = new PluginTypeCatalog<ICatalogProbe>(x => x.Key);
            var first = new HealthTestPluginContext("first", typeof(AlphaProbe), typeof(BetaProbe));
            var second = new HealthTestPluginContext("second", typeof(AlphaTwinProbe));

            // act / validation: the second plugin takes over the key
            catalog.Collect(first);
            catalog.Collect(second);
            Assert.IsType<AlphaTwinProbe>(catalog.Find("alpha"));
            Assert.Same(second, catalog.OwnerOf("alpha"));

            // the first leaves: the second still provides the key, the first's own item goes
            catalog.Withdraw(first);
            Assert.IsType<AlphaTwinProbe>(catalog.Find("alpha"));
            Assert.Null(catalog.Find("beta"));

            // offered again, it arrives last and takes over
            catalog.Collect(first);
            Assert.IsType<AlphaProbe>(catalog.Find("alpha"));

            // the provider leaves: the key falls back to the other offer
            catalog.Withdraw(first);
            Assert.IsType<AlphaTwinProbe>(catalog.Find("alpha"));
            Assert.Same(second, catalog.OwnerOf("alpha"));

            // the last offer leaves: the key leaves
            catalog.Withdraw(second);
            Assert.Null(catalog.Find("alpha"));
            Assert.Empty(catalog.Items);
        }

        /// <summary>
        /// A registered item is nobody's: it competes as an offer, no plugin removal takes it, and
        /// only unregistering does.
        /// </summary>
        [Fact]
        public void Register_SurvivesThePlugins()
        {
            // arrange
            using var catalog = new PluginTypeCatalog<ICatalogProbe>(x => x.Key);
            var plugin = new HealthTestPluginContext("plugin", typeof(AlphaProbe));
            var later = new HealthTestPluginContext("later", typeof(AlphaTwinProbe));
            var registered = new ConstructorProbe("alpha");

            // act / validation
            catalog.Collect(plugin);
            catalog.Register(registered);
            Assert.Same(registered, catalog.Find("alpha"));
            Assert.Null(catalog.OwnerOf("alpha"));

            catalog.Collect(later);
            Assert.IsType<AlphaTwinProbe>(catalog.Find("alpha"));

            catalog.Withdraw(later);
            catalog.Withdraw(plugin);
            Assert.Same(registered, catalog.Find("alpha"));

            var replacement = new ConstructorProbe("ALPHA");
            catalog.Register(replacement);
            Assert.Same(replacement, Assert.Single(catalog.Items));

            Assert.True(catalog.Unregister("alpha"));
            Assert.False(catalog.Unregister("alpha"));
            Assert.Empty(catalog.Items);
            Assert.Throws<ArgumentException>(() => catalog.Register(new ConstructorProbe(" ")));
        }

        /// <summary>
        /// Every change moves the version and raises the events - the leaving item first - after
        /// the lock was released, so a handler may wait for another thread reading the catalog.
        /// </summary>
        [Fact]
        public void Events_AndVersion_FollowEveryChange()
        {
            // arrange
            using var catalog = new PluginTypeCatalog<ICatalogProbe>(x => x.Key);
            var first = new HealthTestPluginContext("first", typeof(AlphaProbe));
            var second = new HealthTestPluginContext("second", typeof(AlphaTwinProbe));
            var events = new List<string>();
            var readable = true;
            catalog.Added += (_, e) =>
            {
                events.Add($"+{e.Key}:{e.PluginContext?.PluginId}");
                readable &= Task.Run(() => catalog.Find(e.Key)).Wait(TimeSpan.FromSeconds(5));
            };
            catalog.Removed += (_, e) => events.Add($"-{e.Key}:{e.PluginContext?.PluginId}");
            var versions = new List<long> { catalog.Version };

            // act
            catalog.Collect(first);
            versions.Add(catalog.Version);
            catalog.Collect(second);
            versions.Add(catalog.Version);
            catalog.Collect(second);
            versions.Add(catalog.Version);
            catalog.Withdraw(second);
            versions.Add(catalog.Version);
            catalog.Dispose();
            versions.Add(catalog.Version);

            // validation
            Assert.True(readable);
            Assert.Equal(["+alpha:first", "-alpha:first", "+alpha:second", "-alpha:second", "+alpha:first", "-alpha:first"], events);
            Assert.True(versions[1] > versions[0]);
            Assert.True(versions[2] > versions[1]);
            Assert.Equal(versions[2], versions[3]);
            Assert.True(versions[4] > versions[3]);
            Assert.True(versions[5] > versions[4]);
        }

        /// <summary>
        /// Items the catalog built are disposed when they leave it; registered items belong to
        /// their caller.
        /// </summary>
        [Fact]
        public void Disposal_ReleasesOnlyWhatTheCatalogBuilt()
        {
            // arrange
            DisposableProbe.Disposed = 0;
            var catalog = new PluginTypeCatalog<ICatalogProbe>(x => x.Key);
            var plugin = new HealthTestPluginContext("plugin", typeof(DisposableProbe));
            var registered = new DisposableProbe { Key = "registered" };

            // act
            catalog.Collect(plugin);
            catalog.Register(registered);
            catalog.Withdraw(plugin);
            var afterWithdraw = DisposableProbe.Disposed;
            catalog.Collect(plugin);
            catalog.Dispose();
            catalog.Collect(plugin);

            // validation
            Assert.Equal(1, afterWithdraw);
            Assert.Equal(2, DisposableProbe.Disposed);
            Assert.Empty(catalog.Items);
        }

        /// <summary>
        /// Attaching sweeps the plugins already loaded and follows the ones arriving later and the
        /// ones leaving; a disposed catalog no longer follows.
        /// </summary>
        [Fact]
        public void Attach_SweepsAndFollowsThePlugins()
        {
            // arrange
            var context = UnitTestFixture.CreateHttpServerContextMock();
            var hub = UnitTestFixture.CreateComponentHubMock(context);
            var plugins = (WebPlugin.PluginManager)hub.PluginManager;
            using var early = new PluginTypeCatalog<IAssemblyProbe>(x => x.Key);
            var disposed = new PluginTypeCatalog<IAssemblyProbe>(x => x.Key);
            early.Attach(hub, context);
            disposed.Attach(hub, context);
            disposed.Dispose();

            // act
            plugins.Register();
            using var late = new PluginTypeCatalog<IAssemblyProbe>(x => x.Key);
            late.Attach(hub, context);
            var found = (early.Find("assembly"), late.Find("assembly"));
            foreach (var plugin in hub.PluginManager.Plugins.ToArray())
            {
                plugins.Remove(plugin);
            }

            // validation
            Assert.IsType<AssemblyProbe>(found.Item1);
            Assert.IsType<AssemblyProbe>(found.Item2);
            Assert.Empty(early.Items);
            Assert.Empty(late.Items);
            Assert.Empty(disposed.Items);
            Assert.Throws<InvalidOperationException>(() => late.Attach(hub, context));
        }

        /// <summary>
        /// An assembly without a plugin is collected for a test that has no plugin manager.
        /// </summary>
        [Fact]
        public void Collect_AnAssemblyWithoutAPlugin()
        {
            // arrange
            using var catalog = new PluginTypeCatalog<IAssemblyProbe>(x => x.Key);

            // act
            catalog.Collect(typeof(AssemblyProbe).Assembly);
            catalog.Collect(typeof(AssemblyProbe).Assembly);

            // validation
            Assert.IsType<AssemblyProbe>(Assert.Single(catalog.Items));
            Assert.Null(catalog.OwnerOf("assembly"));
        }

        /// <summary>
        /// Bound per application, a plugin offers its items to each of its applications separately,
        /// and an application that leaves takes only its own.
        /// </summary>
        [Fact]
        public void ApplicationBinding_KeepsTheItemsPerApplication()
        {
            // arrange
            var first = new ApplicationContext { ApplicationId = "first" };
            var second = new ApplicationContext { ApplicationId = "second" };
            using var catalog = new PluginTypeCatalog<ICatalogProbe>(x => x.Key, typeof(ICatalogProbe),
                origin => new ConstructorProbe("probe") { Application = origin.ApplicationContext }, PluginTypeBinding.Application);
            var plugin = new HealthTestPluginContext("plugin", typeof(AlphaProbe));

            // act
            catalog.Collect(plugin, [first, second, first, null]);
            var forFirst = catalog.Find(first, "probe");
            var forSecond = catalog.Find(second, "probe");
            catalog.Withdraw(first);

            // validation
            Assert.Same(first, ((ConstructorProbe)forFirst).Application);
            Assert.Same(second, ((ConstructorProbe)forSecond).Application);
            Assert.Null(catalog.Find("probe"));
            Assert.Empty(catalog.GetItems(first));
            Assert.Same(forSecond, Assert.Single(catalog.GetItems(second)));
            Assert.Same(plugin, catalog.OwnerOf(second, "probe"));

            catalog.Withdraw(plugin);
            Assert.Empty(catalog.Items);
        }

        /// <summary>
        /// An operation that has no meaning for the binding is refused instead of filing items no
        /// lookup would find.
        /// </summary>
        [Fact]
        public void Binding_RefusesTheOperationsOfTheOther()
        {
            // arrange
            using var perPlugin = new PluginTypeCatalog<ICatalogProbe>(x => x.Key);
            using var perApplication = new PluginTypeCatalog<ICatalogProbe>(x => x.Key, typeof(ICatalogProbe), _ => null, PluginTypeBinding.Application);
            var plugin = new HealthTestPluginContext("plugin", typeof(AlphaProbe));

            // validation
            Assert.Throws<InvalidOperationException>(() => perPlugin.Collect(plugin, [new ApplicationContext()]));
            Assert.Throws<InvalidOperationException>(() => perPlugin.Register(new ApplicationContext(), new AlphaProbe()));
            Assert.Throws<InvalidOperationException>(() => perApplication.Register(new AlphaProbe()));
            Assert.Throws<InvalidOperationException>(() => perApplication.Collect(typeof(AlphaProbe).Assembly));
            Assert.Throws<ArgumentException>(() => new PluginTypeCatalog<AlphaProbe>(x => x.Key, typeof(ICatalogProbe), null));
        }

        /// <summary>
        /// A public type nested in another is not top-level and therefore no item.
        /// </summary>
        public sealed class NestedProbe : ICatalogProbe
        {
            /// <inheritdoc/>
            public string Key => "nested";
        }
    }

    /// <summary>
    /// The contract of the probe items.
    /// </summary>
    public interface ICatalogProbe
    {
        /// <summary>
        /// Gets the key of the item.
        /// </summary>
        string Key { get; }
    }

    /// <summary>
    /// The contract of the probe the test assembly's plugins offer.
    /// </summary>
    public interface IAssemblyProbe
    {
        /// <summary>
        /// Gets the key of the item.
        /// </summary>
        string Key { get; }
    }

    /// <summary>
    /// The one item of the test assembly under <see cref="IAssemblyProbe"/>.
    /// </summary>
    public sealed class AssemblyProbe : IAssemblyProbe
    {
        /// <inheritdoc/>
        public string Key => "assembly";
    }

    /// <summary>
    /// An item following the convention.
    /// </summary>
    public sealed class AlphaProbe : ICatalogProbe
    {
        /// <inheritdoc/>
        public string Key => "alpha";
    }

    /// <summary>
    /// A second item under the key of <see cref="AlphaProbe"/>.
    /// </summary>
    public sealed class AlphaTwinProbe : ICatalogProbe
    {
        /// <inheritdoc/>
        public string Key => "alpha";
    }

    /// <summary>
    /// An item under a key of its own.
    /// </summary>
    public sealed class BetaProbe : ICatalogProbe
    {
        /// <inheritdoc/>
        public string Key => "beta";
    }

    /// <summary>
    /// An unsealed type, which is no item.
    /// </summary>
    public class UnsealedProbe : ICatalogProbe
    {
        /// <inheritdoc/>
        public string Key => "unsealed";
    }

    /// <summary>
    /// An open generic type, which is no item.
    /// </summary>
    /// <typeparam name="T">Unused.</typeparam>
    public sealed class GenericProbe<T> : ICatalogProbe
    {
        /// <inheritdoc/>
        public string Key => "generic";
    }

    /// <summary>
    /// A type without a parameterless constructor, an item only through a factory.
    /// </summary>
    /// <param name="key">The key.</param>
    public sealed class ConstructorProbe(string key) : ICatalogProbe
    {
        /// <inheritdoc/>
        public string Key => key;

        /// <summary>
        /// Gets or sets the application the factory built it for.
        /// </summary>
        public IApplicationContext Application { get; set; }
    }

    /// <summary>
    /// A type whose constructor throws.
    /// </summary>
    public sealed class ThrowingProbe : ICatalogProbe
    {
        /// <summary>
        /// Fails, the way a defective item of a plugin does.
        /// </summary>
        public ThrowingProbe()
        {
            throw new InvalidOperationException("probe constructor failed");
        }

        /// <inheritdoc/>
        public string Key => "throwing";
    }

    /// <summary>
    /// A type that names no key.
    /// </summary>
    public sealed class KeylessProbe : ICatalogProbe
    {
        /// <inheritdoc/>
        public string Key => " ";
    }

    /// <summary>
    /// A disposable item counting its disposals.
    /// </summary>
    public sealed class DisposableProbe : ICatalogProbe, IDisposable
    {
        /// <summary>
        /// Gets or sets the number of disposals of every instance.
        /// </summary>
        public static int Disposed { get; set; }

        /// <inheritdoc/>
        public string Key { get; init; } = "disposable";

        /// <inheritdoc/>
        public void Dispose()
        {
            Disposed++;
        }
    }
}
