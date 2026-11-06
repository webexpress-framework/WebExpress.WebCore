using System.IO.Compression;
using WebExpress.WebCore.WebPackage;

namespace WebExpress.WebCore.Test.Manager
{
    /// <summary>
    /// Unit tests for the package builder.
    /// </summary>
    public sealed class UnitTestPackageBuilder : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "wx-pkgbuilder-" + Guid.NewGuid().ToString("N"));

        /// <summary>
        /// Removes the build tree of the test.
        /// </summary>
        public void Dispose()
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, true);
            }
        }

        /// <summary>
        /// Tests that a Release package takes the Release output, even though a stale Debug
        /// output and the obj copy of the same file are found first in directory order.
        /// </summary>
        [Fact]
        public void ArtifactPrefersRequestedConfiguration()
        {
            // arrange
            Spec("WebExpress", "<artifact>Core.dll</artifact>");
            Write("src/App/bin/Debug/net10.0/Core.dll", "debug");
            Write("src/App/obj/Release/net10.0/Core.dll", "obj");
            Write("src/App/bin/Release/net10.0/Core.dll", "release");

            // act
            var entries = Build("Release");

            // assert
            Assert.Equal("release", entries["bin/Core.dll"]);
        }

        /// <summary>
        /// Tests that the output of another configuration never stands in for a missing one.
        /// </summary>
        [Fact]
        public void ArtifactOfOtherConfigurationOnlyFails()
        {
            // arrange
            Spec("WebExpress", "<artifact>Core.dll</artifact>");
            Write("src/App/bin/Debug/net10.0/Core.dll", "debug");

            // act
            var ex = Assert.Throws<FileNotFoundException>(() => Build("Release"));

            // assert
            Assert.Contains("<artifact>Core.dll</artifact>", ex.Message);
        }

        /// <summary>
        /// Tests that a missing artifact fails and leaves no archive behind.
        /// </summary>
        [Fact]
        public void MissingArtifactFailsWithoutArchive()
        {
            // arrange
            Spec("WebExpress", "<artifact>Present.dll</artifact><artifact>Absent.dll</artifact>");
            Write("bin/Release/net10.0/Present.dll", "x");

            // act
            var ex = Assert.Throws<FileNotFoundException>(() => Build("Release"));

            // assert
            Assert.Contains("Absent.dll", ex.Message);
            Assert.DoesNotContain("Present.dll", ex.Message);
            Assert.False(Directory.Exists(Path.Combine(_root, "pkg")) && Directory.EnumerateFiles(Path.Combine(_root, "pkg")).Any());
        }

        /// <summary>
        /// Tests that a name only matches whole path segments.
        /// </summary>
        [Fact]
        public void ArtifactMatchesWholeFileNameOnly()
        {
            // arrange
            Spec("WebExpress", "<artifact>Core.dll</artifact>");
            Write("bin/Release/net10.0/MyCore.dll", "other");

            // act & assert
            Assert.Throws<FileNotFoundException>(() => Build("Release"));
        }

        /// <summary>
        /// Tests that the lookup stays inside the folder of the spec file.
        /// </summary>
        [Fact]
        public void ReadmeAboveSpecFolderIsNotUsed()
        {
            // arrange
            Spec("WebExpress", "<readme>README.md</readme>", "repo");
            Write("README.md", "foreign");

            // act
            var ex = Assert.Throws<FileNotFoundException>(() => Build("Release", "repo"));

            // assert
            Assert.Contains("<readme>README.md</readme>", ex.Message);
        }

        /// <summary>
        /// Tests that the readme next to the spec wins over one deeper in the tree.
        /// </summary>
        [Fact]
        public void ReadmePrefersShallowestFile()
        {
            // arrange
            Spec("WebExpress", "<readme>README.md</readme>");
            Write("pkg/README.md", "package folder");
            Write("README.md", "root");

            // act
            var entries = Build("Release");

            // assert
            Assert.Equal("root", entries["readme.md"]);
        }

        /// <summary>
        /// Tests that a plugin without build output for a requested target fails.
        /// </summary>
        [Fact]
        public void MissingPluginOutputFails()
        {
            // arrange
            Spec("My.Plugin", "<plugin>src/Plugin</plugin>");
            Write("src/Plugin/bin/Release/net10.0/Plugin.dll", "x");

            // act
            var ex = Assert.Throws<FileNotFoundException>(() => Build("Release", targets: "net10.0;net9.0"));

            // assert
            Assert.Contains("net9.0", ex.Message);
            Assert.DoesNotContain(Path.Combine("Release", "net10.0"), ex.Message);
        }

        /// <summary>
        /// Tests that a plugin package carries the output of every requested target.
        /// </summary>
        [Fact]
        public void PluginOutputIsPacked()
        {
            // arrange
            Spec("My.Plugin", "<plugin>src/Plugin</plugin>");
            Write("src/Plugin/bin/Release/net10.0/Plugin.dll", "plugin");
            Write("src/Plugin/bin/Debug/net10.0/Plugin.dll", "debug");

            // act
            var entries = Build("Release", archive: "My.Plugin.1.0.0.wxp");

            // assert
            Assert.Equal("plugin", entries["lib/Plugin/net10.0/Plugin.dll"]);
            Assert.True(entries.ContainsKey("My.Plugin.spec"));
        }

        /// <summary>
        /// Tests that a license copied to the build output does not add a second entry.
        /// </summary>
        [Fact]
        public void LicenseCopyInOutputIsIgnored()
        {
            // arrange
            Spec("WebExpress", string.Empty);
            Write("licenses/Lib.lic", "source");
            Write("src/App/bin/Release/net10.0/Lib.lic", "copy");

            // act
            var entries = Build("Release");

            // assert
            Assert.Equal("source", entries["licenses/Lib.txt"]);
            Assert.Single(entries.Keys, x => x.StartsWith("licenses/"));
        }

        /// <summary>
        /// Tests that the output of another target framework never stands in for the requested one.
        /// </summary>
        [Fact]
        public void ArtifactOfOtherTargetFrameworkFails()
        {
            // arrange
            Spec("WebExpress", "<artifact>Core.dll</artifact>");
            Write("bin/Release/net8.0/Core.dll", "net8");
            Write("bin/Release/net10.0-windows/Core.dll", "windows");

            // act
            var ex = Assert.Throws<FileNotFoundException>(() => Build("Release"));

            // assert
            Assert.Contains("<artifact>Core.dll</artifact>", ex.Message);
        }

        /// <summary>
        /// Tests that an ordinary subfolder of the configuration output is not taken for a target
        /// framework folder.
        /// </summary>
        [Fact]
        public void ArtifactInOutputSubfolderIsUsed()
        {
            // arrange
            Spec("WebExpress", "<artifact>settings/app.json</artifact>");
            Write("bin/Release/settings/app.json", "output");

            // act
            var entries = Build("Release");

            // assert
            Assert.Equal("output", entries["bin/settings/app.json"]);
        }

        /// <summary>
        /// Tests that an empty plugin output fails, because the package would name the plugin
        /// without carrying any of its files.
        /// </summary>
        [Fact]
        public void EmptyPluginOutputFails()
        {
            // arrange
            Spec("My.Plugin", "<plugin>src/Plugin</plugin>");
            Directory.CreateDirectory(Path.Combine(_root, "src/Plugin/bin/Release/net10.0"));

            // act
            var ex = Assert.Throws<FileNotFoundException>(() => Build("Release", archive: "My.Plugin.1.0.0.wxp"));

            // assert
            Assert.Contains("is empty", ex.Message);
        }

        /// <summary>
        /// Tests that a plugin path cannot reach the build output of a project outside of the spec folder.
        /// </summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void PluginOutsideSpecFolderFails(bool absolute)
        {
            // arrange
            Write("neighbour/bin/Release/net10.0/Foreign.dll", "foreign");
            var plugin = absolute ? Path.Combine(_root, "neighbour") : "../neighbour";
            Spec("My.Plugin", $"<plugin>{plugin}</plugin>", "repo");

            // act
            var ex = Assert.Throws<InvalidOperationException>(() => Build("Release", "repo", archive: "My.Plugin.1.0.0.wxp"));

            // assert
            Assert.Contains("outside of the spec folder", ex.Message);
            Assert.False(Directory.Exists(Path.Combine(_root, "pkg")) && Directory.EnumerateFiles(Path.Combine(_root, "pkg")).Any());
        }

        private void Spec(string id, string elements, string folder = "")
        {
            Write(Path.Combine(folder, "test.spec"),
                $"<?xml version=\"1.0\" encoding=\"utf-8\"?><package><id>{id}</id><version>1.0.0</version>{elements}</package>");
        }

        private void Write(string relativePath, string content)
        {
            var path = Path.Combine(_root, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, content);
        }

        private Dictionary<string, string> Build(string config, string folder = "", string targets = "net10.0", string archive = "WebExpress.1.0.0.zip")
        {
            var output = Path.Combine(_root, "pkg");
            PackageBuilder.Create(Path.Combine(_root, folder, "test.spec"), config, targets, output);

            using var zip = ZipFile.OpenRead(Path.Combine(output, archive));

            return zip.Entries.ToDictionary(x => x.FullName, x =>
            {
                using var reader = new StreamReader(x.Open());
                return reader.ReadToEnd();
            });
        }
    }
}
