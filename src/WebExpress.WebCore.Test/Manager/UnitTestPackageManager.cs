using System.Globalization;
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
            var package = new PackageCatalogItem() { Id = "test", File = "test.wxp", State = PackageCatalogItemState.Active };

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
            var package = new PackageCatalogItem() { Id = "test", File = "test.wxp", State = PackageCatalogItemState.Active };

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
                Assert.Equal(PackageCatalogItemState.Active, packageManager.GetPackage("lifecycle")?.State);

                // act + validation (deactivate)
                var deactivate = packageManager.DeactivatePackage("lifecycle");
                Assert.True(deactivate.Success);
                Assert.Equal(PackageCatalogItemState.Disable, packageManager.GetPackage("lifecycle")?.State);

                // act + validation (activate)
                var activate = packageManager.ActivatePackage("lifecycle");
                Assert.True(activate.Success);
                Assert.Equal(PackageCatalogItemState.Active, packageManager.GetPackage("lifecycle")?.State);

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
        /// Tests that two instances sharing the package directory follow each other's changes:
        /// what one installs, deactivates, activates or uninstalls, the other adopts on its next
        /// scan - while each extracts into a directory of its own.
        /// </summary>
        [Fact]
        public void ClusterInstancesFollowSharedCatalog()
        {
            // arrange
            var packagePath = Path.Combine(Environment.CurrentDirectory, Guid.NewGuid().ToString());
            var statePath = Path.Combine(Path.GetTempPath(), "wx-cluster-" + Guid.NewGuid().ToString("N"));
            var a = CreateClusterInstance(packagePath, statePath);
            var b = CreateClusterInstance(packagePath, statePath);
            var packageFile = Path.Combine(packagePath, "shared.1.0.0.wxp");

            try
            {
                Directory.CreateDirectory(packagePath);
                CreatePackageArchive(packageFile, "shared", "1.0.0");

                // act + validation (install on a, b follows)
                Assert.True(a.InstallPackage(packageFile, true).Success);
                b.Scan();
                Assert.Equal(PackageCatalogItemState.Active, b.GetPackage("shared")?.State);
                Assert.False(Directory.Exists(Path.Combine(packagePath, "shared.1.0.0")), "extraction must not touch the shared directory");

                // act + validation (deactivate on a, b follows)
                Assert.True(a.DeactivatePackage("shared").Success);
                b.Scan();
                Assert.Equal(PackageCatalogItemState.Disable, b.GetPackage("shared")?.State);

                // act + validation (activate on b, a follows)
                Assert.True(b.ActivatePackage("shared").Success);
                a.Scan();
                Assert.Equal(PackageCatalogItemState.Active, a.GetPackage("shared")?.State);

                // act + validation (uninstall on a, b follows)
                Assert.True(a.UninstallPackage("shared").Success);
                b.Scan();
                Assert.Null(b.GetPackage("shared"));
                Assert.Empty(Directory.GetFiles(packagePath, "*.tmp"));
            }
            finally
            {
                foreach (var directory in new[] { packagePath, statePath })
                {
                    if (Directory.Exists(directory))
                    {
                        Directory.Delete(directory, true);
                    }
                }
            }
        }

        /// <summary>
        /// Creates the package manager of one cluster instance over a shared package directory.
        /// </summary>
        /// <param name="packagePath">The shared package directory.</param>
        /// <param name="statePath">The shared cluster state directory.</param>
        /// <returns>The package manager.</returns>
        private static PackageManager CreateClusterInstance(string packagePath, string statePath)
        {
            var context = new HttpServerContext
            (
                new WebExpress.WebCore.WebEndpoint.RouteEndpoint("server"),
                [],
                packagePath,
                Environment.CurrentDirectory,
                Environment.CurrentDirectory,
                Path.Combine(Environment.CurrentDirectory, Guid.NewGuid().ToString()),
                new ConfigurationBuilder().Build(),
                System.Globalization.CultureInfo.GetCultureInfo("en"),
                new WebExpress.WebCore.WebLog.Log() { LogMode = WebExpress.WebCore.WebLog.LogMode.Off },
                null
            );
            var hub = UnitTestFixture.CreateComponentHubMock(context);

            ((WebExpress.WebCore.WebCluster.ClusterManager)hub.ClusterManager).Configure(new ClusterSettings
            {
                NodeId = "node-" + Guid.NewGuid().ToString("N"),
                StatePath = statePath
            });

            return hub.PackageManager as PackageManager;
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
                Assert.Equal(PackageCatalogItemState.Active, package.State);
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
        /// Tests that a built-in plugin cannot be updated - there is no package file to replace
        /// it with - and that the refusal leaves it running.
        /// </summary>
        [Fact]
        public void BuiltInPackageRefusesUpdate()
        {
            // arrange
            var httpServerContext = UnitTestFixture.CreateHttpServerContextMock();
            var componentHub = UnitTestFixture.CreateComponentHubMock(httpServerContext);
            (componentHub.PluginManager as PluginManager).Register();

            var packageManager = componentHub.PackageManager as PackageManager;
            var pluginId = componentHub.PluginManager.Plugins.First().PluginId.ToString();

            // act
            var result = packageManager.UpdatePackage(pluginId, Path.Combine(httpServerContext.PackagePath, "missing.wxp"));

            // validation
            Assert.False(result.Success);
            Assert.Contains("ships with the application", result.Message, StringComparison.OrdinalIgnoreCase);

            // the refusal has to leave the plugin exactly as it was
            Assert.Contains(componentHub.PluginManager.Plugins, x => x.PluginId.ToString() == pluginId);
            Assert.Equal(PackageCatalogItemState.Active, packageManager.GetPackage(pluginId)?.State);
        }

        /// <summary>
        /// Uninstalling a built-in plugin withdraws it at once and records its assembly, which is
        /// locked while the process runs, for deletion on the next start.
        /// </summary>
        [Fact]
        public void BuiltInPluginUninstallWithdrawsItAndRecordsItsFile()
        {
            // arrange
            var httpServerContext = UnitTestFixture.CreateHttpServerContextMock();
            var componentHub = UnitTestFixture.CreateComponentHubMock(httpServerContext);
            (componentHub.PluginManager as PluginManager).Register();

            var packageManager = componentHub.PackageManager as PackageManager;
            var plugin = componentHub.PluginManager.Plugins.First();
            var pluginId = plugin.PluginId.ToString();
            var file = Path.GetFileName(plugin.Assembly.Location);
            var packagePath = httpServerContext.PackagePath;

            try
            {
                Directory.CreateDirectory(packagePath);

                // act
                var result = packageManager.UninstallPackage(pluginId);

                // validation
                Assert.True(result.Success, result.Message);
                Assert.DoesNotContain(componentHub.PluginManager.Plugins, x => x.PluginId.ToString() == pluginId);
                Assert.Null(packageManager.GetPackage(pluginId));
                Assert.Contains($"<removed>{file}</removed>", File.ReadAllText(Path.Combine(packagePath, "catalog.xml")), StringComparison.Ordinal);
                Assert.True(File.Exists(plugin.Assembly.Location), "the file is only deleted on the next start");
            }
            finally
            {
                Directory.Delete(packagePath, true);
            }
        }

        /// <summary>
        /// On the next start the recorded assembly of an uninstalled built-in plugin is deleted
        /// before the plugins are loaded, and its record goes with it.
        /// </summary>
        [Fact]
        public void RemovedBuiltInFileIsDeletedOnStart()
        {
            // arrange
            var httpServerContext = UnitTestFixture.CreateHttpServerContextMock();
            var componentHub = UnitTestFixture.CreateComponentHubMock(httpServerContext);
            var packageManager = componentHub.PackageManager as PackageManager;
            var file = $"Removed.{Guid.NewGuid():N}.dll";
            var path = Path.Combine(AppContext.BaseDirectory, file);
            File.WriteAllText(path, "not an assembly");
            packageManager.Catalog.RemovedPlugins.Add(file);

            try
            {
                // act
                typeof(PackageManager)
                    .GetMethod("DeleteRemovedBuiltIns", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(packageManager, null);

                // validation
                Assert.False(File.Exists(path));
                Assert.DoesNotContain(file, packageManager.Catalog.RemovedPlugins);
            }
            finally
            {
                File.Delete(path);
            }
        }

        /// <summary>
        /// A built-in plugin is deactivated by withdrawing its plugin and activated by registering
        /// it again from its loaded assembly; it stays listed in between, and the decision is
        /// recorded in the catalog so that it outlives the process.
        /// </summary>
        [Fact]
        public void BuiltInPluginCanBeDeactivatedAndActivatedAgain()
        {
            // arrange
            var httpServerContext = UnitTestFixture.CreateHttpServerContextMock();
            var componentHub = UnitTestFixture.CreateComponentHubMock(httpServerContext);
            (componentHub.PluginManager as PluginManager).Register();

            var packageManager = componentHub.PackageManager as PackageManager;
            var pluginId = componentHub.PluginManager.Plugins.First().PluginId.ToString();
            var packagePath = httpServerContext.PackagePath;
            var catalogFile = Path.Combine(packagePath, "catalog.xml");

            try
            {
                Directory.CreateDirectory(packagePath);

                // act + validation (deactivated: withdrawn, still listed, recorded)
                var deactivated = packageManager.DeactivatePackage(pluginId);
                Assert.True(deactivated.Success, deactivated.Message);
                Assert.DoesNotContain(componentHub.PluginManager.Plugins, x => x.PluginId.ToString() == pluginId);
                Assert.Equal(PackageCatalogItemState.Disable, packageManager.GetPackage(pluginId)?.State);
                Assert.True(packageManager.GetPackage(pluginId)?.BuiltIn);
                Assert.Contains($"<disabled>{pluginId}</disabled>", File.ReadAllText(catalogFile), StringComparison.Ordinal);

                // act + validation (activated: registered again, no longer recorded)
                var activated = packageManager.ActivatePackage(pluginId);
                Assert.True(activated.Success, activated.Message);
                Assert.Contains(componentHub.PluginManager.Plugins, x => x.PluginId.ToString() == pluginId);
                Assert.Equal(PackageCatalogItemState.Active, packageManager.GetPackage(pluginId)?.State);
                Assert.DoesNotContain("<disabled>", File.ReadAllText(catalogFile), StringComparison.Ordinal);
                Assert.Single(packageManager.GetPackages(), x => x.Id == pluginId);
            }
            finally
            {
                Directory.Delete(packagePath, true);
            }
        }

        /// <summary>
        /// A system plugin carries the framework the applications - and the management surface -
        /// are built on, so it is refused before anything of it is touched.
        /// </summary>
        [Fact]
        public void SystemPluginCannotBeSwitched()
        {
            // arrange
            var httpServerContext = UnitTestFixture.CreateHttpServerContextMock();
            var componentHub = UnitTestFixture.CreateComponentHubMock(httpServerContext);
            (componentHub.PluginManager as PluginManager).Register();

            var packageManager = componentHub.PackageManager as PackageManager;
            var pluginId = componentHub.PluginManager.Plugins.First().PluginId.ToString();
            packageManager.GetPackage(pluginId).SystemPlugin = true;

            // act
            var deactivated = packageManager.DeactivatePackage(pluginId);
            var activated = packageManager.ActivatePackage(pluginId);

            // validation
            Assert.False(deactivated.Success);
            Assert.Contains("system plugin", deactivated.Message, StringComparison.OrdinalIgnoreCase);
            Assert.False(activated.Success);
            Assert.Contains(componentHub.PluginManager.Plugins, x => x.PluginId.ToString() == pluginId);
            Assert.Equal(PackageCatalogItemState.Active, packageManager.GetPackage(pluginId)?.State);
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
        /// A package may only configure its own plugin: a settings file that sets any key outside
        /// of Plugins:&lt;plugin-id&gt; - a server section, another plugin, or a sibling id that
        /// merely starts with the same name - is refused as a whole and never deployed.
        /// </summary>
        [Theory]
        [InlineData("""{ "Http": { "Port": "8080" } }""")]
        [InlineData("""{ "Plugins": { "other": { "Greeting": "hello" } } }""")]
        [InlineData("""{ "Plugins": { "withsettingsx": { "Greeting": "hello" } } }""")]
        [InlineData("""{ "Plugins:other:Greeting": "hello" }""")]
        [InlineData("""{ "Plugins": "hello" }""")]
        [InlineData("""{ "Plugins": { "withsettings": { "Greeting": "hello" } }, "Security": { "Enabled": "false" } }""")]
        public void InstallRefusesSettingsOutsideOwnPluginSection(string settings)
        {
            // arrange
            var settingsPath = Path.Combine(Environment.CurrentDirectory, Guid.NewGuid().ToString());
            var configuration = new ConfigurationBuilder().AddSettingsDirectory(settingsPath, reloadOnChange: false).Build();
            var httpServerContext = UnitTestFixture.CreateHttpServerContextMock(settingsPath, configuration);
            var componentHub = UnitTestFixture.CreateComponentHubMock(httpServerContext);
            var packageManager = componentHub.PackageManager as PackageManager;
            var packagePath = httpServerContext.PackagePath;
            var packageFile = Path.Combine(packagePath, "withsettings.1.0.0.wxp");

            try
            {
                Directory.CreateDirectory(packagePath);
                CreatePackageArchive(packageFile, "withsettings", "1.0.0", settings);

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
        /// Configuration keys are case-insensitive, so a settings file that addresses its own
        /// plugin section in different casing - or through a colon-separated property name - is
        /// still within scope and deployed.
        /// </summary>
        [Theory]
        [InlineData("""{ "plugins": { "WithSettings": { "Greeting": "hello" } } }""")]
        [InlineData("""{ "Plugins:withsettings": { "Greeting": "hello" } }""")]
        public void InstallDeploysSettingsOfOwnPluginSection(string settings)
        {
            // arrange
            var settingsPath = Path.Combine(Environment.CurrentDirectory, Guid.NewGuid().ToString());
            var configuration = new ConfigurationBuilder().AddSettingsDirectory(settingsPath, reloadOnChange: false).Build();
            var httpServerContext = UnitTestFixture.CreateHttpServerContextMock(settingsPath, configuration);
            var componentHub = UnitTestFixture.CreateComponentHubMock(httpServerContext);
            var packageManager = componentHub.PackageManager as PackageManager;
            var packagePath = httpServerContext.PackagePath;
            var packageFile = Path.Combine(packagePath, "withsettings.1.0.0.wxp");

            try
            {
                Directory.CreateDirectory(packagePath);
                CreatePackageArchive(packageFile, "withsettings", "1.0.0", settings);

                // act
                var install = packageManager.InstallPackage(packageFile, true);

                // validation
                Assert.True(install.Success);
                Assert.Equal("hello", configuration.GetPluginSettings("withsettings")["Greeting"]);
            }
            finally
            {
                Directory.Delete(packagePath, true);

                if (Directory.Exists(settingsPath))
                {
                    Directory.Delete(settingsPath, true);
                }
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
        /// A refused upload must not replace an installed package that has the same file name:
        /// the upload is checked before it reaches the package directory.
        /// </summary>
        [Fact]
        public void RefusedUploadKeepsTheInstalledPackageOfTheSameName()
        {
            // arrange
            var httpServerContext = UnitTestFixture.CreateHttpServerContextMock();
            var componentHub = UnitTestFixture.CreateComponentHubMock(httpServerContext);
            var packageManager = componentHub.PackageManager as PackageManager;
            var packagePath = httpServerContext.PackagePath;
            var packageFile = Path.Combine(packagePath, "keep.wxp");

            try
            {
                Directory.CreateDirectory(packagePath);
                CreatePackageArchive(packageFile, "keep", "1.0.0");
                Assert.True(packageManager.InstallPackage(packageFile).Success);

                // act
                using var garbage = new MemoryStream("not-a-package"u8.ToArray());
                var upload = packageManager.UploadPackage(garbage, "keep.wxp");

                // validation
                Assert.False(upload.Success);
                Assert.True(packageManager.ValidatePackage(packageFile).IsValid, "the installed file must be untouched");
                Assert.Equal(PackageCatalogItemState.Active, packageManager.GetPackage("keep")?.State);
                Assert.False(Directory.Exists(Path.Combine(packagePath, PackageManager.StagingDirectory)), "the staging directory is cleaned up");
            }
            finally
            {
                Directory.Delete(packagePath, true);
            }
        }

        /// <summary>
        /// Uploading a package under another file name than the installed one is an update of
        /// that package, not a second package with the same id.
        /// </summary>
        [Fact]
        public void UploadWithKnownIdReplacesThePackage()
        {
            // arrange
            var httpServerContext = UnitTestFixture.CreateHttpServerContextMock();
            var componentHub = UnitTestFixture.CreateComponentHubMock(httpServerContext);
            var packageManager = componentHub.PackageManager as PackageManager;
            var packagePath = httpServerContext.PackagePath;
            var source = Path.Combine(Environment.CurrentDirectory, Guid.NewGuid().ToString());

            try
            {
                Directory.CreateDirectory(packagePath);
                Directory.CreateDirectory(source);
                CreatePackageArchive(Path.Combine(source, "same.1.0.0.wxp"), "same", "1.0.0");
                CreatePackageArchive(Path.Combine(source, "same.2.0.0.wxp"), "same", "2.0.0");

                // act
                using (var first = File.OpenRead(Path.Combine(source, "same.1.0.0.wxp")))
                {
                    Assert.True(packageManager.UploadPackage(first, "same.1.0.0.wxp").Success);
                }

                using (var second = File.OpenRead(Path.Combine(source, "same.2.0.0.wxp")))
                {
                    Assert.True(packageManager.UploadPackage(second, "same.2.0.0.wxp").Success);
                }

                // validation
                var package = Assert.Single(packageManager.Catalog.Packages, x => x.Id == "same");
                Assert.Equal("same.2.0.0.wxp", package.File);
                Assert.Equal("2.0.0", package.Metadata.Version);
                Assert.False(File.Exists(Path.Combine(packagePath, "same.1.0.0.wxp")));
            }
            finally
            {
                Directory.Delete(packagePath, true);
                Directory.Delete(source, true);
            }
        }

        /// <summary>
        /// A file the scan finds that carries the id of an installed package is a copy and is
        /// not taken in as a second package.
        /// </summary>
        [Fact]
        public void ScanIgnoresSecondFileWithInstalledId()
        {
            // arrange
            var httpServerContext = UnitTestFixture.CreateHttpServerContextMock();
            var componentHub = UnitTestFixture.CreateComponentHubMock(httpServerContext);
            var packageManager = componentHub.PackageManager as PackageManager;
            var packagePath = httpServerContext.PackagePath;

            try
            {
                Directory.CreateDirectory(packagePath);
                CreatePackageArchive(Path.Combine(packagePath, "twin.1.0.0.wxp"), "twin", "1.0.0");
                packageManager.Scan();
                CreatePackageArchive(Path.Combine(packagePath, "twin-copy.wxp"), "twin", "1.0.0");

                // act
                packageManager.Scan();

                // validation
                Assert.Single(packageManager.Catalog.Packages, x => x.Id == "twin");
            }
            finally
            {
                Directory.Delete(packagePath, true);
            }
        }

        /// <summary>
        /// An update with the version of the installed package is carried out: the new content
        /// is loaded instead of being taken for an unchanged file.
        /// </summary>
        [Fact]
        public void UpdateWithSameVersionReloadsThePackage()
        {
            // arrange
            var httpServerContext = UnitTestFixture.CreateHttpServerContextMock();
            var componentHub = UnitTestFixture.CreateComponentHubMock(httpServerContext);
            var packageManager = componentHub.PackageManager as PackageManager;
            var packagePath = httpServerContext.PackagePath;
            var source = Path.Combine(Environment.CurrentDirectory, Guid.NewGuid().ToString());

            try
            {
                Directory.CreateDirectory(packagePath);
                Directory.CreateDirectory(source);
                CreatePackageArchive(Path.Combine(packagePath, "reload.wxp"), "reload", "1.0.0");
                CreatePackageArchive(Path.Combine(source, "reload.wxp"), "reload", "1.0.0", title: "rebuilt");
                Assert.True(packageManager.InstallPackage(Path.Combine(packagePath, "reload.wxp")).Success);

                // act
                using var stream = File.OpenRead(Path.Combine(source, "reload.wxp"));
                var update = packageManager.UpdatePackage("reload", stream, "reload.wxp");

                // validation
                Assert.True(update.Success, update.Message);
                Assert.Equal("rebuilt", packageManager.GetPackage("reload")?.Metadata?.Title);
                Assert.Equal(PackageCatalogItemState.Active, packageManager.GetPackage("reload")?.State);
            }
            finally
            {
                Directory.Delete(packagePath, true);
                Directory.Delete(source, true);
            }
        }

        /// <summary>
        /// An update that cannot start leaves the running version in place instead of
        /// unloading it first and failing afterwards.
        /// </summary>
        [Fact]
        public void UpdateWithMissingDependencyKeepsTheRunningVersion()
        {
            // arrange
            var httpServerContext = UnitTestFixture.CreateHttpServerContextMock();
            var componentHub = UnitTestFixture.CreateComponentHubMock(httpServerContext);
            var packageManager = componentHub.PackageManager as PackageManager;
            var packagePath = httpServerContext.PackagePath;
            var source = Path.Combine(Environment.CurrentDirectory, Guid.NewGuid().ToString());

            try
            {
                Directory.CreateDirectory(packagePath);
                Directory.CreateDirectory(source);
                CreatePackageArchive(Path.Combine(packagePath, "stable.1.0.0.wxp"), "stable", "1.0.0");
                CreatePackageArchive(Path.Combine(source, "stable.2.0.0.wxp"), "stable", "2.0.0", dependencies: ["absent"]);
                Assert.True(packageManager.InstallPackage(Path.Combine(packagePath, "stable.1.0.0.wxp")).Success);

                // act
                var update = packageManager.UpdatePackage("stable", Path.Combine(source, "stable.2.0.0.wxp"));

                // validation
                Assert.False(update.Success);
                Assert.Contains("absent", update.Message, StringComparison.Ordinal);
                Assert.Equal(PackageCatalogItemState.Active, packageManager.GetPackage("stable")?.State);
                Assert.Equal("1.0.0", packageManager.GetPackage("stable")?.Metadata?.Version);
                Assert.True(File.Exists(Path.Combine(packagePath, "stable.1.0.0.wxp")));
                Assert.False(File.Exists(Path.Combine(packagePath, "stable.2.0.0.wxp")));
            }
            finally
            {
                Directory.Delete(packagePath, true);
                Directory.Delete(source, true);
            }
        }

        /// <summary>
        /// A package installed before its dependency waits and starts as soon as the dependency
        /// is installed; while it runs, the dependency cannot be removed. Deactivating the
        /// dependency stops it along, and it starts again once the dependency is back.
        /// </summary>
        [Fact]
        public void DependentPackageWaitsForItsDependencyAndHoldsIt()
        {
            // arrange
            var httpServerContext = UnitTestFixture.CreateHttpServerContextMock();
            var componentHub = UnitTestFixture.CreateComponentHubMock(httpServerContext);
            var packageManager = componentHub.PackageManager as PackageManager;
            var packagePath = httpServerContext.PackagePath;

            try
            {
                Directory.CreateDirectory(packagePath);
                CreatePackageArchive(Path.Combine(packagePath, "child.wxp"), "child", "1.0.0", dependencies: ["parent>=1.0.0"]);
                CreatePackageArchive(Path.Combine(packagePath, "parent.wxp"), "parent", "1.0.0");

                // act + validation (the child waits)
                var child = packageManager.InstallPackage(Path.Combine(packagePath, "child.wxp"));
                Assert.True(child.Success, child.Message);
                Assert.Equal(PackageCatalogItemState.Available, packageManager.GetPackage("child")?.State);
                Assert.Contains("parent", packageManager.GetPackage("child")?.Problem, StringComparison.Ordinal);

                // act + validation (the parent arrives, the child starts with it)
                Assert.True(packageManager.InstallPackage(Path.Combine(packagePath, "parent.wxp")).Success);
                Assert.Equal(PackageCatalogItemState.Active, packageManager.GetPackage("child")?.State);
                Assert.Null(packageManager.GetPackage("child")?.Problem);

                // act + validation (the parent cannot be removed under the running child)
                Assert.Equal(new[] { "child" }, packageManager.GetDependentPackages("parent").Select(x => x.Id));
                Assert.False(packageManager.UninstallPackage("parent").Success);
                Assert.Equal(PackageCatalogItemState.Active, packageManager.GetPackage("parent")?.State);

                // act + validation (deactivating the parent stops the child, which waits)
                var deactivated = packageManager.DeactivatePackage("parent");
                Assert.True(deactivated.Success, deactivated.Message);
                Assert.Contains("child", deactivated.Message, StringComparison.Ordinal);
                Assert.Equal(PackageCatalogItemState.Available, packageManager.GetPackage("child")?.State);
                Assert.Contains("parent", packageManager.GetPackage("child")?.Problem, StringComparison.Ordinal);

                // act + validation (the child comes back with the parent)
                Assert.True(packageManager.ActivatePackage("parent").Success);
                Assert.Equal(PackageCatalogItemState.Active, packageManager.GetPackage("child")?.State);
            }
            finally
            {
                Directory.Delete(packagePath, true);
            }
        }

        /// <summary>
        /// The reason a package waits and the messages of the operations are recorded without
        /// a request, yet read in the language of whoever reads them rather than the server's.
        /// </summary>
        [Fact]
        public void ProblemAndMessagesAreTranslatedForTheReader()
        {
            // arrange
            var httpServerContext = UnitTestFixture.CreateHttpServerContextMock();
            var componentHub = UnitTestFixture.CreateComponentHubMock(httpServerContext);
            var packageManager = componentHub.PackageManager as PackageManager;
            var packagePath = httpServerContext.PackagePath;
            var german = CultureInfo.GetCultureInfo("de");
            var english = CultureInfo.GetCultureInfo("en");

            try
            {
                Directory.CreateDirectory(packagePath);
                CreatePackageArchive(Path.Combine(packagePath, "child.wxp"), "child", "1.0.0", dependencies: ["parent"]);
                File.WriteAllText(Path.Combine(packagePath, "broken.wxp"), "no archive");

                // act
                packageManager.InstallPackage(Path.Combine(packagePath, "child.wxp"));
                var child = packageManager.GetPackage("child");
                var notFound = packageManager.ActivatePackage("unknown");
                var invalid = packageManager.InstallPackage(Path.Combine(packagePath, "broken.wxp"));

                // validation
                Assert.Equal("Wartet auf: parent", child.GetProblem(german));
                Assert.Equal("Waiting for: parent", child.GetProblem(english));
                Assert.Contains("nicht gefunden", notFound.GetMessage(german), StringComparison.Ordinal);
                Assert.Contains("not found", notFound.GetMessage(english), StringComparison.Ordinal);
                Assert.NotEqual(invalid.GetMessage(english), invalid.GetMessage(german));
            }
            finally
            {
                Directory.Delete(packagePath, true);
            }
        }

        /// <summary>
        /// When the file of a dependency disappears, its dependents are unloaded with it and
        /// wait for it, rather than running on an assembly that is gone.
        /// </summary>
        [Fact]
        public void RemovedDependencyUnloadsItsDependents()
        {
            // arrange
            var httpServerContext = UnitTestFixture.CreateHttpServerContextMock();
            var componentHub = UnitTestFixture.CreateComponentHubMock(httpServerContext);
            var packageManager = componentHub.PackageManager as PackageManager;
            var packagePath = httpServerContext.PackagePath;

            try
            {
                Directory.CreateDirectory(packagePath);
                CreatePackageArchive(Path.Combine(packagePath, "base.wxp"), "base", "1.0.0");
                CreatePackageArchive(Path.Combine(packagePath, "addon.wxp"), "addon", "1.0.0", dependencies: ["base"]);
                packageManager.Scan();
                Assert.Equal(PackageCatalogItemState.Active, packageManager.GetPackage("addon")?.State);

                // act
                File.Delete(Path.Combine(packagePath, "base.wxp"));
                packageManager.Scan();

                // validation
                Assert.Null(packageManager.GetPackage("base"));
                Assert.Equal(PackageCatalogItemState.Available, packageManager.GetPackage("addon")?.State);
            }
            finally
            {
                Directory.Delete(packagePath, true);
            }
        }

        /// <summary>
        /// A catalog entry without a file name must not lead to the package directory being
        /// removed: an empty name resolves to the directory itself.
        /// </summary>
        [Fact]
        public void CatalogEntryWithoutFileNameLeavesThePackageDirectoryAlone()
        {
            // arrange
            var httpServerContext = UnitTestFixture.CreateHttpServerContextMock();
            var componentHub = UnitTestFixture.CreateComponentHubMock(httpServerContext);
            var packageManager = componentHub.PackageManager as PackageManager;
            var packagePath = httpServerContext.PackagePath;
            var load = typeof(PackageManager).GetMethod("LoadCatalog", BindingFlags.NonPublic | BindingFlags.Instance);
            var remove = typeof(PackageManager).GetMethod("RemoveExtractedDirectory", BindingFlags.NonPublic | BindingFlags.Instance);

            try
            {
                Directory.CreateDirectory(packagePath);
                CreatePackageArchive(Path.Combine(packagePath, "bystander.wxp"), "bystander", "1.0.0");
                File.WriteAllText(Path.Combine(packagePath, "catalog.xml"), """<catalog><package id="broken" file="" state="Active" /></catalog>""");

                // act
                load.Invoke(packageManager, null);
                packageManager.Scan();
                remove.Invoke(packageManager, [new PackageCatalogItem() { Id = "broken", File = string.Empty }]);

                // validation
                Assert.DoesNotContain(packageManager.Catalog.Packages, x => x.Id == "broken");
                Assert.True(File.Exists(Path.Combine(packagePath, "bystander.wxp")));
                Assert.True(File.Exists(Path.Combine(packagePath, "catalog.xml")));
            }
            finally
            {
                Directory.Delete(packagePath, true);
            }
        }

        /// <summary>
        /// A package whose plugin carries the id of a registered plugin is refused, and the
        /// registered plugin survives the refusal - before, the package took over the id and
        /// unloading the package removed the plugin it collided with.
        /// </summary>
        [Fact]
        public void PackageWithTakenPluginIdLeavesTheRegisteredPluginAlone()
        {
            // arrange
            var httpServerContext = UnitTestFixture.CreateHttpServerContextMock();
            var componentHub = UnitTestFixture.CreateComponentHubMock(httpServerContext);
            (componentHub.PluginManager as PluginManager).Register();

            var packageManager = componentHub.PackageManager as PackageManager;
            var packagePath = httpServerContext.PackagePath;
            var packageFile = Path.Combine(packagePath, "intruder.wxp");
            var assembly = typeof(UnitTestPackageManager).Assembly;
            var assemblyName = assembly.GetName().Name;
            var tfm = (string)typeof(PackageManager).GetMethod("GetTFM", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
            var registered = componentHub.PluginManager.Plugins.Single(x => x.Assembly == assembly);

            try
            {
                Directory.CreateDirectory(packagePath);
                CreatePackageArchive(packageFile, "intruder", "1.0.0", plugins: [$"lib/{assemblyName}"], files: new Dictionary<string, string>
                {
                    [$"lib/{assemblyName}/{tfm}/{assemblyName}.dll"] = assembly.Location
                });

                // act
                var install = packageManager.InstallPackage(packageFile);

                // validation
                Assert.False(install.Success);
                Assert.Contains("could not be registered", install.Message, StringComparison.Ordinal);
                Assert.Null(packageManager.GetPackage("intruder"));
                Assert.Same(registered, componentHub.PluginManager.GetPlugin(registered.PluginId.ToString()));
            }
            finally
            {
                DeleteUnloadedDirectory(packagePath);
            }
        }

        /// <summary>
        /// Deletes a directory holding assemblies a collectible load context loaded. Windows
        /// keeps such a file locked until the runtime has collected the unloaded context.
        /// </summary>
        /// <param name="directory">The directory.</param>
        private static void DeleteUnloadedDirectory(string directory)
        {
            for (var attempt = 0; ; attempt++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();

                try
                {
                    Directory.Delete(directory, true);

                    return;
                }
                catch (UnauthorizedAccessException) when (attempt < 10)
                {
                    Thread.Sleep(50);
                }
            }
        }

        /// <summary>
        /// A package may configure its own plugins, which are named after their plugin sources,
        /// even when the package id differs from them.
        /// </summary>
        [Fact]
        public void InstallDeploysSettingsOfAPluginOfThePackage()
        {
            // arrange
            var settingsPath = Path.Combine(Environment.CurrentDirectory, Guid.NewGuid().ToString());
            var configuration = new ConfigurationBuilder().AddSettingsDirectory(settingsPath, reloadOnChange: false).Build();
            var httpServerContext = UnitTestFixture.CreateHttpServerContextMock(settingsPath, configuration);
            var componentHub = UnitTestFixture.CreateComponentHubMock(httpServerContext);
            var packageManager = componentHub.PackageManager as PackageManager;
            var packagePath = httpServerContext.PackagePath;
            var packageFile = Path.Combine(packagePath, "bundle.wxp");

            try
            {
                Directory.CreateDirectory(packagePath);
                CreatePackageArchive(packageFile, "bundle", "1.0.0", """{ "Plugins": { "My.Plugin": { "Greeting": "hello" } } }""", plugins: ["lib/My.Plugin"]);

                // act
                var install = packageManager.InstallPackage(packageFile, true);

                // validation
                Assert.True(install.Success, install.Message);
                Assert.Equal("hello", configuration.GetPluginSettings("my.plugin")["Greeting"]);
            }
            finally
            {
                Directory.Delete(packagePath, true);

                if (Directory.Exists(settingsPath))
                {
                    Directory.Delete(settingsPath, true);
                }
            }
        }

        /// <summary>
        /// The deployed settings file survives a plain uninstallation and is removed only when
        /// that is asked for.
        /// </summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void UninstallRemovesSettingsOnlyWhenAsked(bool removeSettings)
        {
            // arrange
            var settingsPath = Path.Combine(Environment.CurrentDirectory, Guid.NewGuid().ToString());
            var configuration = new ConfigurationBuilder().AddSettingsDirectory(settingsPath, reloadOnChange: false).Build();
            var httpServerContext = UnitTestFixture.CreateHttpServerContextMock(settingsPath, configuration);
            var componentHub = UnitTestFixture.CreateComponentHubMock(httpServerContext);
            var packageManager = componentHub.PackageManager as PackageManager;
            var packagePath = httpServerContext.PackagePath;
            var packageFile = Path.Combine(packagePath, "withsettings.1.0.0.wxp");
            var settingsFile = Path.Combine(settingsPath, "withsettings.settings.json");

            try
            {
                Directory.CreateDirectory(packagePath);
                CreatePackageArchive(packageFile, "withsettings", "1.0.0", """{ "Plugins": { "withsettings": { "Greeting": "hello" } } }""");
                Assert.True(packageManager.InstallPackage(packageFile, true).Success);
                Assert.True(File.Exists(settingsFile));

                // act
                var uninstall = packageManager.UninstallPackage("withsettings", removeSettings);

                // validation
                Assert.True(uninstall.Success);
                Assert.Equal(!removeSettings, File.Exists(settingsFile));
            }
            finally
            {
                Directory.Delete(packagePath, true);
                Directory.Delete(settingsPath, true);
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
        /// <param name="dependencies">The dependency expressions of the package.</param>
        /// <param name="plugins">The plugin sources of the package.</param>
        /// <param name="title">The title, if it is to differ from the default.</param>
        /// <param name="files">Further entries, from entry name to the file they are copied from.</param>
        private static void CreatePackageArchive(string file, string id, string version, string settings = null, string settingsEntry = null, string[] dependencies = null, string[] plugins = null, string title = null, IDictionary<string, string> files = null)
        {
            using var zip = ZipFile.Open(file, ZipArchiveMode.Create);
            var specEntry = zip.CreateEntry($"{id}.spec");
            using (var writer = new StreamWriter(specEntry.Open()))
            {
                writer.Write($@"
                    <package>
                        <id>{id}</id>
                        <version>{version}</version>
                        <title>{title ?? id + "-title"}</title>
                        <authors>UnitTest</authors>
                        {string.Concat((plugins ?? []).Select(x => $"<plugin>{x}</plugin>"))}
                        {string.Concat((dependencies ?? []).Select(x => $"<dependency>{x}</dependency>"))}
                    </package>");
            }

            // add minimal lib folder marker to resemble package layout
            zip.CreateEntry("lib/");

            foreach (var (entry, source) in files ?? new Dictionary<string, string>())
            {
                zip.CreateEntryFromFile(source, entry);
            }

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
