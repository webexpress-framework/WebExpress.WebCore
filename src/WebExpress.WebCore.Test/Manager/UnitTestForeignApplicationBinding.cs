using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using WebExpress.WebCore.Test.Fixture;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebPage;
using WebExpress.WebCore.WebPlugin;
using WebExpress.WebCore.WebSitemap;

namespace WebExpress.WebCore.Test.Manager
{
    /// <summary>
    /// Tests that a plugin which names an application of another plugin through an application
    /// attribute serves its endpoints under that application, although the application is not
    /// defined in the plugin's own assembly.
    /// </summary>
    public class UnitTestForeignApplicationBinding
    {
        private const string AssemblyName = "WebExpress.WebCore.Test.Foreign";
        private const string AssetContent = "/* foreign */";

        /// <summary>
        /// Tests that page, fragment and asset of a plugin loaded after the foreign application
        /// are bound to that application. A contributing plugin serves its pages under its name and its
        /// assets under its id, below the application's context path.
        /// </summary>
        [Fact]
        public void PluginAfterApplication()
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();
            var pluginManager = componentHub.PluginManager as PluginManager;
            var foreign = ForeignAssembly.Create();
            var application = componentHub.ApplicationManager.GetApplications(typeof(TestApplicationA)).Single();

            // act
            var plugins = pluginManager.Register(foreign).ToList();
            componentHub.SitemapManager.Refresh();

            // validation
            Assert.Single(plugins);
            Assert.Contains(application, componentHub.PluginManager.GetAssociatedApplications(plugins[0]));
            Assert.Equal("webexpress.webcore.test.foreign.www.extension", Search(componentHub, "http://localhost:8080/server/appa/foreign/extension"));
            Assert.Single(componentHub.FragmentManager.GetFragments(application, foreign.Fragment));
            Assert.Equal(AssetContent, Request(componentHub, "http://localhost:8080/server/appa/webexpress.webcore.test.foreign/assets/css/foreign.css"));
        }

        /// <summary>
        /// Tests that the plugin is not bound to an application it does not name.
        /// </summary>
        [Fact]
        public void UnnamedApplicationStaysUnbound()
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();
            var pluginManager = componentHub.PluginManager as PluginManager;
            var foreign = ForeignAssembly.Create();
            var application = componentHub.ApplicationManager.GetApplications(typeof(TestApplicationB)).Single();

            // act
            pluginManager.Register(foreign);
            componentHub.SitemapManager.Refresh();

            // validation
            Assert.Empty(componentHub.FragmentManager.GetFragments(application, foreign.Fragment));
            Assert.Null(Search(componentHub, "http://localhost:8080/server/appb/foreign/extension"));
        }

        /// <summary>
        /// Resolves a uri through the sitemap.
        /// </summary>
        /// <param name="componentHub">The hub to search.</param>
        /// <param name="uri">The requested uri.</param>
        /// <returns>The endpoint id, or null when the uri is not served.</returns>
        private static string Search(IComponentHub componentHub, string uri)
        {
            var httpServerContext = UnitTestFixture.CreateHttpServerContextMock();
            var result = componentHub.SitemapManager.SearchResource(new System.Uri(uri), new SearchContext()
            {
                HttpServerContext = httpServerContext,
                Culture = httpServerContext.Culture,
                HttpContext = UnitTestFixture.CreateHttpContextMock()
            });

            return result?.EndpointContext?.EndpointId?.ToString();
        }

        /// <summary>
        /// Requests a uri and returns the body as text.
        /// </summary>
        /// <param name="componentHub">The hub that answers.</param>
        /// <param name="uri">The requested uri.</param>
        /// <returns>The response body, or null when nothing answers.</returns>
        private static string Request(IComponentHub componentHub, string uri)
        {
            var httpServerContext = UnitTestFixture.CreateHttpServerContextMock();
            var result = componentHub.SitemapManager.SearchResource(new System.Uri(uri), new SearchContext()
            {
                HttpServerContext = httpServerContext,
                Culture = httpServerContext.Culture,
                HttpContext = UnitTestFixture.CreateHttpContextMock()
            });

            if (result?.EndpointContext is null)
            {
                return null;
            }

            var response = componentHub.EndpointManager.HandleRequest(UnitTestFixture.CreateRequestMock("", uri), result.EndpointContext);

            return response is ResponseNotFound ? null : Encoding.UTF8.GetString(response.Content as byte[]);
        }

        /// <summary>
        /// A plugin assembly of its own, emitted at test time. Its types cannot live in the test
        /// assembly, because the test plugin would discover them and bind them to every test application.
        /// </summary>
        private sealed class ForeignAssembly : Assembly
        {
            private readonly Assembly _emitted;
            private readonly Type[] _types;

            /// <summary>
            /// Gets the emitted fragment type.
            /// </summary>
            public Type Fragment { get; }

            /// <summary>
            /// Initializes a new instance of the class.
            /// </summary>
            /// <param name="emitted">The dynamic assembly that holds the types.</param>
            /// <param name="types">The emitted types.</param>
            /// <param name="fragment">The emitted fragment type.</param>
            private ForeignAssembly(Assembly emitted, Type[] types, Type fragment)
            {
                _emitted = emitted;
                _types = types;
                Fragment = fragment;
            }

            /// <summary>
            /// Emits the plugin, a page under WWW and a fragment, each a sealed subclass of a base
            /// in the test assembly.
            /// </summary>
            /// <returns>The plugin assembly.</returns>
            public static ForeignAssembly Create()
            {
                var builder = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName(AssemblyName), AssemblyBuilderAccess.Run);
                var module = builder.DefineDynamicModule(AssemblyName);

                var plugin = Emit(module, $"{AssemblyName}.ForeignPlugin", typeof(ForeignPluginBase),
                    Attribute(typeof(ApplicationAttribute<TestApplicationA>)),
                    new CustomAttributeBuilder(typeof(NameAttribute).GetConstructor([typeof(string)]), ["Foreign"]));
                var page = Emit(module, $"{AssemblyName}.WWW.Extension", typeof(ForeignPageBase));
                var fragment = Emit(module, $"{AssemblyName}.ForeignFragment", typeof(ForeignFragmentBase), Attribute(typeof(SectionAttribute<TestSectionA>)));

                return new ForeignAssembly(builder, [plugin, page, fragment], fragment);
            }

            /// <summary>
            /// Emits a public sealed subclass with a public parameterless constructor.
            /// </summary>
            /// <param name="module">The dynamic module.</param>
            /// <param name="name">The full name of the type.</param>
            /// <param name="parent">The base type.</param>
            /// <param name="attributes">The attributes of the type.</param>
            /// <returns>The created type.</returns>
            private static Type Emit(ModuleBuilder module, string name, Type parent, params CustomAttributeBuilder[] attributes)
            {
                var type = module.DefineType(name, TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.Class, parent);
                type.DefineDefaultConstructor(MethodAttributes.Public);

                foreach (var attribute in attributes)
                {
                    type.SetCustomAttribute(attribute);
                }

                return type.CreateType();
            }

            /// <summary>
            /// Describes an attribute with a parameterless constructor.
            /// </summary>
            /// <param name="attributeType">The attribute type.</param>
            /// <returns>The attribute description.</returns>
            private static CustomAttributeBuilder Attribute(Type attributeType)
            {
                return new CustomAttributeBuilder(attributeType.GetConstructor(Type.EmptyTypes), []);
            }

            /// <summary>
            /// Gets the name of the emitted assembly.
            /// </summary>
            public override string FullName => _emitted.FullName;

            /// <summary>
            /// Gets no location, since the assembly exists in memory only.
            /// </summary>
            public override string Location => string.Empty;

            /// <summary>
            /// Returns the name of the emitted assembly.
            /// </summary>
            /// <returns>The assembly name.</returns>
            public override AssemblyName GetName() => _emitted.GetName();

            /// <summary>
            /// Returns the name of the emitted assembly.
            /// </summary>
            /// <param name="copiedName">Whether the codebase is the copied one.</param>
            /// <returns>The assembly name.</returns>
            public override AssemblyName GetName(bool copiedName) => _emitted.GetName(copiedName);

            /// <summary>
            /// Returns the emitted types, which discovery reads.
            /// </summary>
            /// <returns>The types.</returns>
            public override Type[] GetTypes() => _types;

            /// <summary>
            /// Returns the emitted types, which plugin and application discovery read.
            /// </summary>
            /// <returns>The types.</returns>
            public override Type[] GetExportedTypes() => _types;

            /// <summary>
            /// Returns no assembly attributes.
            /// </summary>
            /// <param name="inherit">Ignored.</param>
            /// <returns>An empty array.</returns>
            public override object[] GetCustomAttributes(bool inherit) => Array.Empty<Attribute>();

            /// <summary>
            /// Returns no assembly attributes, as an array of the asked type, which the attribute helpers cast to.
            /// </summary>
            /// <param name="attributeType">The attribute type.</param>
            /// <param name="inherit">Ignored.</param>
            /// <returns>An empty array of the type.</returns>
            public override object[] GetCustomAttributes(Type attributeType, bool inherit) => (object[])Array.CreateInstance(attributeType, 0);

            /// <summary>
            /// Tells that no assembly attribute is defined.
            /// </summary>
            /// <param name="attributeType">The attribute type.</param>
            /// <param name="inherit">Ignored.</param>
            /// <returns>False.</returns>
            public override bool IsDefined(Type attributeType, bool inherit) => false;

            /// <summary>
            /// Returns no assembly attribute data.
            /// </summary>
            /// <returns>An empty list.</returns>
            public override IList<CustomAttributeData> GetCustomAttributesData() => [];

            /// <summary>
            /// Returns the one embedded asset of the plugin.
            /// </summary>
            /// <returns>The resource names.</returns>
            public override string[] GetManifestResourceNames() => [$"{AssemblyName}.Assets.css\\foreign.css"];

            /// <summary>
            /// Returns the content of the embedded asset.
            /// </summary>
            /// <param name="name">The resource name.</param>
            /// <returns>The content, or null for another name.</returns>
            public override Stream GetManifestResourceStream(string name)
            {
                return name == $"{AssemblyName}.Assets.css\\foreign.css"
                    ? new MemoryStream(Encoding.UTF8.GetBytes(AssetContent))
                    : null;
            }
        }
    }

    /// <summary>
    /// The base of the emitted foreign plugin. Abstract, so the test plugin does not discover it.
    /// </summary>
    public abstract class ForeignPluginBase : IPlugin
    {
        /// <summary>
        /// Called when the plugin starts working.
        /// </summary>
        public void Run()
        {
        }

        /// <summary>
        /// Releases the plugin.
        /// </summary>
        public void Dispose()
        {
        }
    }

    /// <summary>
    /// The base of the emitted foreign page. Abstract, so the test plugin does not discover it.
    /// </summary>
    public abstract class ForeignPageBase : Page<TestVisualTree>
    {
        /// <summary>
        /// Processing of the page.
        /// </summary>
        /// <param name="renderContext">The context for rendering the page.</param>
        /// <param name="visualTree">The visual tree to be rendered.</param>
        public override void Process(IRenderContext renderContext, TestVisualTree visualTree)
        {
        }

        /// <summary>
        /// Releases the page.
        /// </summary>
        public override void Dispose()
        {
        }
    }

    /// <summary>
    /// The base of the emitted foreign fragment. Abstract, so the test plugin does not discover it.
    /// </summary>
    public abstract class ForeignFragmentBase : IFragment
    {
        /// <summary>
        /// Gets the id.
        /// </summary>
        public string Id => string.Empty;

        /// <summary>
        /// Renders the fragment.
        /// </summary>
        /// <param name="renderContext">The context in which rendering occurs.</param>
        /// <param name="visualTree">The visual tree used for rendering the fragment.</param>
        /// <returns>The fragment's markup.</returns>
        public IHtmlNode Render(IRenderContext renderContext, IVisualTree visualTree)
        {
            return new HtmlText("ForeignFragment");
        }

        /// <summary>
        /// Releases the fragment.
        /// </summary>
        public void Dispose()
        {
        }
    }
}
