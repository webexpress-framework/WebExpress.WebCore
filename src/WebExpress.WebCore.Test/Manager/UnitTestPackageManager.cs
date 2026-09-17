using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using WebExpress.WebCore.Test.Fixture;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebCore.WebPackage;
using WebExpress.WebCore.WebPackage.Model;
using WebExpress.WebCore.WebPlugin;
using WebExpress.WebCore.WebSetting;

namespace WebExpress.WebCore.Test.Manager
{
    /// <summary>
    /// Unit tests for the package manager.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestPackageManager
    {
        /// <summary>
        /// Tests the register function of the package manager.
        /// </summary>
        [Fact]
        public void Register()
        {
            // arrange
            var componentHub = UnitTestFixture.CreateComponentHubMock();
            var packageManager = componentHub.PackageManager as PackageManager;

            // act
            Assert.NotNull(packageManager);
        }

        /// <summary>
        /// Tests the remove function of the package manager.
        /// </summary>
        [Fact]
        public void Remove()
        {
            // arrange
            var componentHub = UnitTestFixture.CreateComponentHubMock();
            var packageManager = componentHub.PackageManager as PackageManager;

            // act
            Assert.NotNull(packageManager);
        }

        /// <summary>
        /// Tests whether the package manager implements interface IComponentManager.
        /// </summary>
        [Fact]
        public void IsIComponentManager()
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();
            var packageManager = componentHub.PackageManager as PackageManager;

            // act
            Assert.True(typeof(IComponentManager).IsAssignableFrom(packageManager.GetType()));
        }

        /// <summary>
        /// Tests adding a package and firing the AddPackage event.
        /// </summary>
        [Fact]
        public void AddPackageEvent()
        {
            // arrange
            var componentHub = UnitTestFixture.CreateComponentHubMock();
            var packageManager = componentHub.PackageManager as PackageManager;
            bool eventFired = false;
            packageManager.AddPackage += (sender, item) => { eventFired = true; };

            // create dummy package
            var package = new PackageCatalogItem() { Id = "test", File = "test.wxp", State = PackageCatalogeItemState.Active };

            // act
            var method = typeof(PackageManager).GetMethod("OnAddPackage", BindingFlags.NonPublic | BindingFlags.Instance);
            method.Invoke(packageManager, [package]);

            // validation
            Assert.True(eventFired);
        }

        /// <summary>
        /// Tests removing a package and firing the RemovePackage event.
        /// </summary>
        [Fact]
        public void RemovePackageEvent()
        {
            // arrange
            var componentHub = UnitTestFixture.CreateComponentHubMock();
            var packageManager = componentHub.PackageManager as PackageManager;
            bool eventFired = false;
            packageManager.RemovePackage += (sender, item) => { eventFired = true; };

            // create dummy package
            var package = new PackageCatalogItem() { Id = "test", File = "test.wxp", State = PackageCatalogeItemState.Active };

            // act
            var method = typeof(PackageManager).GetMethod("OnRemovePackage", BindingFlags.NonPublic | BindingFlags.Instance);
            method.Invoke(packageManager, [package]);

            Assert.True(eventFired);
        }

        /// <summary>
        /// Tests that a package can be added, scanned and detected as new.
        /// </summary>
        [Fact]
        public void ScanDetectsNewPackage()
        {
            // arrange
            var httpServerContext = UnitTestFixture.CreateHttpServerContextMock();
            var componentHub = UnitTestFixture.CreateComponentHubMock(httpServerContext);
            var packageManager = componentHub.PackageManager as PackageManager;
            var packagePath = httpServerContext.PackagePath;
            var dummyFile = Path.Combine(packagePath, "dummy.wxp");

            try
            {
                // create dummy package zip file with valid .spec inside
                Directory.CreateDirectory(packagePath);

                using (var zip = ZipFile.Open(dummyFile, ZipArchiveMode.Create))
                {
                    var entry = zip.CreateEntry("dummy.spec");
                    using var writer = new StreamWriter(entry.Open());
                    writer.Write(@"
                    <package>
                        <id>dummy</id>
                        <version>1.0.0</version>
                        <title>DummyTitle</title>
                        <authors>UnitTest</authors>
                    </package>");
                }

                // act - scan should detect the new file
                packageManager.Scan();

                // validation
                Assert.Contains(packageManager.Catalog.Packages, x => x.File == "dummy.wxp");

            }
            finally
            {
                // cleanup
                File.Delete(dummyFile);
                Directory.Delete(packagePath, true);
            }
        }

        /// <summary>
        /// Tests that removing a package file triggers its removal from the catalog.
        /// </summary>
        [Fact]
        public void ScanDetectsRemovedPackage()
        {
            // arrange
            var httpServerContext = UnitTestFixture.CreateHttpServerContextMock();
            var componentHub = UnitTestFixture.CreateComponentHubMock(httpServerContext);
            var packageManager = componentHub.PackageManager as PackageManager;
            var packagePath = httpServerContext.PackagePath;
            var dummyFile = Path.Combine(packagePath, "dummy.wxp");

            try
            {
                // place and scan dummy package file
                Directory.CreateDirectory(packagePath);

                using (var zip = ZipFile.Open(dummyFile, ZipArchiveMode.Create))
                {
                    var entry = zip.CreateEntry("dummy.spec");
                    using var writer = new StreamWriter(entry.Open());
                    writer.Write(@"
                    <package>
                        <id>dummy</id>
                        <version>1.0.0</version>
                        <title>DummyTitle</title>
                        <authors>UnitTest</authors>
                    </package>");
                }

                packageManager.Scan();
                Assert.Contains(packageManager.Catalog.Packages, x => x.File == "dummy.wxp");

                // remove file and scan again
                File.Delete(dummyFile);

                // act - scan should detect the removed file
                packageManager.Scan();

                // validation
                Assert.DoesNotContain(packageManager.Catalog.Packages, x => x.File == "dummy.wxp");

            }
            finally
            {
                // cleanup
                File.Delete(dummyFile);
                Directory.Delete(packagePath, true);
            }
        }

        /// <summary>
        /// Tests loading package metadata from a package file.
        /// </summary>
        [Fact]
        public void LoadPackageReadsSpec()
        {
            // arrange
            var httpServerContext = UnitTestFixture.CreateHttpServerContextMock();
            var componentHub = UnitTestFixture.CreateComponentHubMock(httpServerContext);
            var packageManager = componentHub.PackageManager as PackageManager;
            var packagePath = httpServerContext.PackagePath;
            var dummyFile = Path.Combine(packagePath, "dummy.wxp");

            try
            {
                // create minimal dummy .wxp with .spec inside
                Directory.CreateDirectory(packagePath);

                using (var zip = ZipFile.Open(dummyFile, ZipArchiveMode.Create))
                {
                    var entry = zip.CreateEntry("dummy.spec");
                    using var writer = new StreamWriter(entry.Open());
                    writer.Write(@"
                    <package>
                        <id>dummy</id>
                        <version>1.0.0</version>
                        <title>DummyTitle</title>
                        <authors>UnitTest</authors>
                    </package>");
                }
                // use private LoadPackage method via reflection
                var method = typeof(PackageManager).GetMethod("LoadPackage", BindingFlags.NonPublic | BindingFlags.Instance);

                // act
                var result = method.Invoke(packageManager, [dummyFile]) as PackageCatalogItem;

                // validation
                Assert.NotNull(result);
                Assert.Equal("dummy", result?.Id);
                Assert.Equal("DummyTitle", result?.Metadata.Title);
            }
            finally
            {
                // cleanup
                File.Delete(dummyFile);
                Directory.Delete(packagePath, true);
            }
        }

        /// <summary>
        /// Tests package validation with extension/type checks.
        /// </summary>
        [Fact]
        public void ValidatePackageRejectsInvalidExtension()
        {
            // arrange
            var httpServerContext = UnitTestFixture.CreateHttpServerContextMock();
            var componentHub = UnitTestFixture.CreateComponentHubMock(httpServerContext);
            var packageManager = componentHub.PackageManager as PackageManager;
            var packagePath = httpServerContext.PackagePath;
            var dummyFile = Path.Combine(packagePath, "dummy.zip");

            try
            {
                Directory.CreateDirectory(packagePath);
                File.WriteAllText(dummyFile, "not-a-package");

                // act
                var validation = packageManager.ValidatePackage(dummyFile);

                // validation
                Assert.False(validation.IsValid);
                Assert.Contains(validation.Messages, x => x.Contains("extension", StringComparison.OrdinalIgnoreCase));
            }
            finally
            {
                if (File.Exists(dummyFile))
                {
                    File.Delete(dummyFile);
                }

                if (Directory.Exists(packagePath))
                {
                    Directory.Delete(packagePath, true);
                }
            }
        }

        /// <summary>
        /// Tests a complete package lifecycle using explicit package manager operations.
        /// </summary>
        [Fact]
        public void PackageLifecycleInstallDeactivateActivateUninstall()
        {
            // arrange
            var httpServerContext = UnitTestFixture.CreateHttpServerContextMock();
            var componentHub = UnitTestFixture.CreateComponentHubMock(httpServerContext);
            var packageManager = componentHub.PackageManager as PackageManager;
            var packagePath = httpServerContext.PackagePath;
            var packageFile = Path.Combine(packagePath, "lifecycle.1.0.0.wxp");

            try
            {
                Directory.CreateDirectory(packagePath);
                CreatePackageArchive(packageFile, "lifecycle", "1.0.0");

                // act + validation (install active)
                var install = packageManager.InstallPackage(packageFile, true);
                Assert.True(install.Success);
                Assert.Equal(PackageCatalogeItemState.Active, packageManager.GetPackage("lifecycle")?.State);

                // act + validation (deactivate)
                var deactivate = packageManager.DeactivatePackage("lifecycle");
                Assert.True(deactivate.Success);
                Assert.Equal(PackageCatalogeItemState.Disable, packageManager.GetPackage("lifecycle")?.State);

                // act + validation (activate)
                var activate = packageManager.ActivatePackage("lifecycle");
                Assert.True(activate.Success);
                Assert.Equal(PackageCatalogeItemState.Active, packageManager.GetPackage("lifecycle")?.State);

                // act + validation (uninstall)
                var uninstall = packageManager.UninstallPackage("lifecycle");
                Assert.True(uninstall.Success);
                Assert.Null(packageManager.GetPackage("lifecycle"));
            }
            finally
            {
                if (Directory.Exists(packagePath))
                {
                    Directory.Delete(packagePath, true);
                }
            }
        }

        /// <summary>
        /// Tests that update fails when uploaded package id does not match the requested package id.
        /// </summary>
        [Fact]
        public void UpdatePackageRejectsMismatchedId()
        {
            // arrange
            var httpServerContext = UnitTestFixture.CreateHttpServerContextMock();
            var componentHub = UnitTestFixture.CreateComponentHubMock(httpServerContext);
            var packageManager = componentHub.PackageManager as PackageManager;
            var packagePath = httpServerContext.PackagePath;
            var packageFile = Path.Combine(packagePath, "other.1.0.0.wxp");

            try
            {
                Directory.CreateDirectory(packagePath);
                CreatePackageArchive(packageFile, "other", "1.0.0");

                // act
                var result = packageManager.UpdatePackage("expected", packageFile);

                // validation
                Assert.False(result.Success);
                Assert.Contains("does not match", result.Message, StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                if (Directory.Exists(packagePath))
                {
                    Directory.Delete(packagePath, true);
                }
            }
        }

        /// <summary>
        /// Tests package SHA-256 verification support.
        /// </summary>
        [Fact]
        public void ValidatePackageWithSha256()
        {
            // arrange
            var httpServerContext = UnitTestFixture.CreateHttpServerContextMock();
            var componentHub = UnitTestFixture.CreateComponentHubMock(httpServerContext);
            var packageManager = componentHub.PackageManager as PackageManager;
            var packagePath = httpServerContext.PackagePath;
            var packageFile = Path.Combine(packagePath, "signed.1.0.0.wxp");

            try
            {
                Directory.CreateDirectory(packagePath);
                CreatePackageArchive(packageFile, "signed", "1.0.0");
                var expectedHash = ComputeSha256(packageFile);

                // act
                var valid = packageManager.ValidatePackage(packageFile, expectedSha256: expectedHash);
                var invalid = packageManager.ValidatePackage(packageFile, expectedSha256: "deadbeef");

                // validation
                Assert.True(valid.IsValid);
                Assert.False(invalid.IsValid);
            }
            finally
            {
                if (Directory.Exists(packagePath))
                {
                    Directory.Delete(packagePath, true);
                }
            }
        }

        /// <summary>
        /// Tests that the plugins loaded from the application directory are reported by the read
        /// side even though they are not packages.
        /// </summary>
        /// <remarks>
        /// This is the defect the built-in entries exist for: in a plain build deployment every
        /// plugin is referenced statically, the catalog is empty, and a management surface reading
        /// the catalog alone stays blank while the server logs the plugins as running.
        /// </remarks>
        [Fact]
        public void GetPackagesReportsStaticallyLoadedPluginsAsBuiltIn()
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();
            var packageManager = componentHub.PackageManager as PackageManager;
            var pluginIds = componentHub.PluginManager.Plugins.Select(x => x.PluginId.ToString()).ToList();

            // act
            var packages = packageManager.GetPackages().ToList();

            // validation
            Assert.NotEmpty(pluginIds);
            Assert.Empty(packageManager.Catalog.Packages);

            foreach (var pluginId in pluginIds)
            {
                var package = Assert.Single(packages, x => x.Id == pluginId);

                Assert.True(package.BuiltIn);
                Assert.Equal(string.Empty, package.File);
                Assert.Equal(PackageCatalogeItemState.Active, package.State);
                Assert.Contains(package.Plugins, x => x.PluginId.ToString() == pluginId);
                Assert.Equal(pluginId, package.Metadata?.Id);
            }
        }

        /// <summary>
        /// Tests that a plugin present both statically and as an installed package is reported
        /// once, by the package - which is the entry the lifecycle operations can act on.
        /// </summary>
        [Fact]
        public void GetPackagesReportsAPluginOnceWhenItIsAlsoInstalled()
        {
            // arrange
            var httpServerContext = UnitTestFixture.CreateHttpServerContextMock();
            var componentHub = UnitTestFixture.CreateComponentHubMock(httpServerContext);
            (componentHub.PluginManager as PluginManager).Register();

            var packageManager = componentHub.PackageManager as PackageManager;
            var pluginId = componentHub.PluginManager.Plugins.First().PluginId.ToString();
            var packagePath = httpServerContext.PackagePath;
            var packageFile = Path.Combine(packagePath, $"{pluginId}.1.0.0.wxp");

            try
            {
                Directory.CreateDirectory(packagePath);
                CreatePackageArchive(packageFile, pluginId, "1.0.0");

                // act
                var install = packageManager.InstallPackage(packageFile, true);
                var packages = packageManager.GetPackages().Where(x => x.Id == pluginId).ToList();

                // validation
                Assert.True(install.Success);
                Assert.False(Assert.Single(packages).BuiltIn);
            }
            finally
            {
                if (Directory.Exists(packagePath))
                {
                    Directory.Delete(packagePath, true);
                }
            }
        }

        /// <summary>
        /// Tests that the built-in entries never reach the persisted catalog.
        /// </summary>
        /// <remarks>
        /// Persisting them would be worse than the original defect: on the next start LoadCatalog
        /// would read them back as installed packages, every path built from their empty file name
        /// would fail to resolve, and Scan would count them as no longer present and drop them.
        /// </remarks>
        [Fact]
        public void SaveCatalogLeavesBuiltInEntriesOutOfTheCatalogFile()
        {
            // arrange
            var httpServerContext = UnitTestFixture.CreateHttpServerContextMock();
            var componentHub = UnitTestFixture.CreateComponentHubMock(httpServerContext);
            (componentHub.PluginManager as PluginManager).Register();

            var packageManager = componentHub.PackageManager as PackageManager;
            var packagePath = httpServerContext.PackagePath;
            var catalogFile = Path.Combine(packagePath, "catalog.xml");
            var save = typeof(PackageManager).GetMethod("SaveCatalog", BindingFlags.NonPublic | BindingFlags.Instance);
            var load = typeof(PackageManager).GetMethod("LoadCatalog", BindingFlags.NonPublic | BindingFlags.Instance);

            try
            {
                Directory.CreateDirectory(packagePath);
                Assert.NotEmpty(packageManager.GetPackages());

                // act - two start cycles worth of save/load must not adopt the built-ins
                save.Invoke(packageManager, null);
                load.Invoke(packageManager, null);
                save.Invoke(packageManager, null);

                // validation
                Assert.Empty(packageManager.Catalog.Packages);
                Assert.DoesNotContain("<package", File.ReadAllText(catalogFile), StringComparison.Ordinal);
                Assert.NotEmpty(packageManager.GetPackages());
            }
            finally
            {
                if (Directory.Exists(packagePath))
                {
                    Directory.Delete(packagePath, true);
                }
            }
        }

        /// <summary>
        /// Tests that the directory scan neither adopts nor removes the built-in entries, which
        /// only exist in the read path.
        /// </summary>
        [Fact]
        public void ScanKeepsBuiltInEntriesOutOfTheCatalog()
        {
            // arrange
            var httpServerContext = UnitTestFixture.CreateHttpServerContextMock();
            var componentHub = UnitTestFixture.CreateComponentHubMock(httpServerContext);
            (componentHub.PluginManager as PluginManager).Register();

            var packageManager = componentHub.PackageManager as PackageManager;
            var packagePath = httpServerContext.PackagePath;

            try
            {
                Directory.CreateDirectory(packagePath);

                // act
                packageManager.Scan();
                packageManager.Scan();

                // validation
                Assert.Empty(packageManager.Catalog.Packages);
                Assert.All(packageManager.GetPackages(), x => Assert.True(x.BuiltIn));
            }
            finally
            {
                if (Directory.Exists(packagePath))
                {
                    Directory.Delete(packagePath, true);
                }
            }
        }

        /// <summary>
        /// Tests that every lifecycle operation refuses a built-in plugin with a defined failure
        /// rather than running half of its steps.
        /// </summary>
        [Fact]
        public void BuiltInPackageRefusesEveryLifecycleOperation()
        {
            // arrange
            var httpServerContext = UnitTestFixture.CreateHttpServerContextMock();
            var componentHub = UnitTestFixture.CreateComponentHubMock(httpServerContext);
            (componentHub.PluginManager as PluginManager).Register();

            var packageManager = componentHub.PackageManager as PackageManager;
            var pluginId = componentHub.PluginManager.Plugins.First().PluginId.ToString();

            // act
            var results = new[]
            {
                packageManager.ActivatePackage(pluginId),
                packageManager.DeactivatePackage(pluginId),
                packageManager.UpdatePackage(pluginId, Path.Combine(httpServerContext.PackagePath, "missing.wxp")),
                packageManager.UninstallPackage(pluginId)
            };

            // validation
            Assert.All(results, x =>
            {
                Assert.False(x.Success);
                Assert.Contains("ships with the application", x.Message, StringComparison.OrdinalIgnoreCase);
            });

            // the refusal has to leave the plugin exactly as it was
            Assert.Contains(componentHub.PluginManager.Plugins, x => x.PluginId.ToString() == pluginId);
            Assert.Equal(PackageCatalogeItemState.Active, packageManager.GetPackage(pluginId)?.State);
        }

        /// <summary>
        /// A package ships its settings file under settings/; installing it deploys the file to
        /// the settings directory of the server and reloads the configuration, so the plugin's
        /// section is there before the plugin boots.
        /// </summary>
        [Fact]
        public void InstallDeploysSettingsFileAndReloadsConfiguration()
        {
            // arrange
            var settingsPath = Path.Combine(Environment.CurrentDirectory, Guid.NewGuid().ToString());
            var configuration = new ConfigurationBuilder().AddSettingsDirectory(settingsPath, reloadOnChange: false).Build();
            var httpServerContext = UnitTestFixture.CreateHttpServerContextMock(settingsPath, configuration);
            var componentHub = UnitTestFixture.CreateComponentHubMock(httpServerContext);
            var packageManager = componentHub.PackageManager as PackageManager;
            var packagePath = httpServerContext.PackagePath;
            var packageFile = Path.Combine(packagePath, "withsettings.1.0.0.wxp");
            var section = configuration.GetPluginSettings("withsettings");

            try
            {
                Directory.CreateDirectory(packagePath);
                CreatePackageArchive(packageFile, "withsettings", "1.0.0", """{ "Plugins": { "withsettings": { "Greeting": "hello" } } }""");

                // act
                var install = packageManager.InstallPackage(packageFile, true);

                // validation
                Assert.True(install.Success);
                Assert.True(File.Exists(Path.Combine(settingsPath, "withsettings.settings.json")));
                Assert.Equal("hello", section["Greeting"]);
            }
            finally
            {
                Directory.Delete(packagePath, true);
                Directory.Delete(settingsPath, true);
            }
        }

        /// <summary>
        /// A settings file the administrator already has is theirs: a package update must not
        /// overwrite it with the shipped default.
        /// </summary>
        [Fact]
        public void InstallKeepsExistingSettingsFile()
        {
            // arrange
            var settingsPath = Path.Combine(Environment.CurrentDirectory, Guid.NewGuid().ToString());
            Directory.CreateDirectory(settingsPath);
            File.WriteAllText(Path.Combine(settingsPath, "withsettings.settings.json"), """{ "Plugins": { "withsettings": { "Greeting": "edited" } } }""");
            var configuration = new ConfigurationBuilder().AddSettingsDirectory(settingsPath, reloadOnChange: false).Build();
            var httpServerContext = UnitTestFixture.CreateHttpServerContextMock(settingsPath, configuration);
            var componentHub = UnitTestFixture.CreateComponentHubMock(httpServerContext);
            var packageManager = componentHub.PackageManager as PackageManager;
            var packagePath = httpServerContext.PackagePath;
            var packageFile = Path.Combine(packagePath, "withsettings.1.0.0.wxp");

            try
            {
                Directory.CreateDirectory(packagePath);
                CreatePackageArchive(packageFile, "withsettings", "1.0.0", """{ "Plugins": { "withsettings": { "Greeting": "shipped" } } }""");

                // act
                var install = packageManager.InstallPackage(packageFile, true);

                // validation
                Assert.True(install.Success);
                Assert.Equal("edited", configuration.GetPluginSettings("withsettings")["Greeting"]);
            }
            finally
            {
                Directory.Delete(packagePath, true);
                Directory.Delete(settingsPath, true);
            }
        }

        /// <summary>
        /// A settings file that does not parse is refused: once in the directory it would fail
        /// every following start of the server, so the installation goes on without it.
        /// </summary>
        [Fact]
        public void InstallRefusesInvalidSettingsFile()
        {
            // arrange
            var settingsPath = Path.Combine(Environment.CurrentDirectory, Guid.NewGuid().ToString());
            var configuration = new ConfigurationBuilder().AddSettingsDirectory(settingsPath, reloadOnChange: false).Build();
            var httpServerContext = UnitTestFixture.CreateHttpServerContextMock(settingsPath, configuration);
            var componentHub = UnitTestFixture.CreateComponentHubMock(httpServerContext);
            var packageManager = componentHub.PackageManager as PackageManager;
            var packagePath = httpServerContext.PackagePath;
            var packageFile = Path.Combine(packagePath, "broken.1.0.0.wxp");

            try
            {
                Directory.CreateDirectory(packagePath);
                CreatePackageArchive(packageFile, "broken", "1.0.0", "{ this is not json");

                // act
                var install = packageManager.InstallPackage(packageFile, true);

                // validation
                Assert.True(install.Success);
                Assert.False(Directory.Exists(settingsPath));
            }
            finally
            {
                Directory.Delete(packagePath, true);
            }
        }

        /// <summary>
        /// Only a json file directly below settings/ is a settings file; an entry that tries to
        /// leave the directory is ignored and never written anywhere.
        /// </summary>
        [Fact]
        public void InstallIgnoresNestedSettingsEntry()
        {
            // arrange
            var settingsPath = Path.Combine(Environment.CurrentDirectory, Guid.NewGuid().ToString());
            var configuration = new ConfigurationBuilder().AddSettingsDirectory(settingsPath, reloadOnChange: false).Build();
            var httpServerContext = UnitTestFixture.CreateHttpServerContextMock(settingsPath, configuration);
            var componentHub = UnitTestFixture.CreateComponentHubMock(httpServerContext);
            var packageManager = componentHub.PackageManager as PackageManager;
            var packagePath = httpServerContext.PackagePath;
            var packageFile = Path.Combine(packagePath, "nested.1.0.0.wxp");

            try
            {
                Directory.CreateDirectory(packagePath);
                CreatePackageArchive(packageFile, "nested", "1.0.0", null, "settings/sub/escape.json");

                // act
                var install = packageManager.InstallPackage(packageFile, true);

                // validation
                Assert.True(install.Success);
                Assert.False(Directory.Exists(settingsPath));
            }
            finally
            {
                Directory.Delete(packagePath, true);
            }
        }

        /// <summary>
        /// Creates a simple package archive for tests.
        /// </summary>
        /// <param name="file">The package file path.</param>
        /// <param name="id">The package id.</param>
        /// <param name="version">The package version.</param>
        /// <param name="settings">The content of a settings file shipped as settings/{id}.settings.json, or null for none.</param>
        /// <param name="settingsEntry">The entry name of the settings file, if it is to differ from the default.</param>
        private static void CreatePackageArchive(string file, string id, string version, string settings = null, string settingsEntry = null)
        {
            using var zip = ZipFile.Open(file, ZipArchiveMode.Create);
            var specEntry = zip.CreateEntry($"{id}.spec");
            using (var writer = new StreamWriter(specEntry.Open()))
            {
                writer.Write($@"
                    <package>
                        <id>{id}</id>
                        <version>{version}</version>
                        <title>{id}-title</title>
                        <authors>UnitTest</authors>
                    </package>");
            }

            // add minimal lib folder marker to resemble package layout
            zip.CreateEntry("lib/");

            if (settings is not null || settingsEntry is not null)
            {
                var entry = zip.CreateEntry(settingsEntry ?? $"settings/{id}.settings.json");
                using var writer = new StreamWriter(entry.Open());
                writer.Write(settings ?? "{}");
            }
        }

        /// <summary>
        /// Computes SHA-256 for a test file.
        /// </summary>
        /// <param name="file">The file path.</param>
        /// <returns>The sha-256 hash as lowercase hex.</returns>
        private static string ComputeSha256(string file)
        {
            using var stream = File.OpenRead(file);
            var hash = SHA256.HashData(stream);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }
    }
}
