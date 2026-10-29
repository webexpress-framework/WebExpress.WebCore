using WebExpress.WebCore.Test.Fixture;
using WebExpress.WebCore.WebEndpoint;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebResource;
using WebExpress.WebCore.WebUri;

namespace WebExpress.WebCore.Test.WebResource
{
    /// <summary>
    /// Tests the resource that serves files from a directory on disk.
    /// </summary>
    public class UnitTestResourceFile
    {
        /// <summary>
        /// A file resource serving its own directory, rooted wherever the test puts it.
        /// </summary>
        private sealed class TestResourceFile : ResourceFile
        {
            /// <summary>
            /// Initializes a new instance of the class.
            /// </summary>
            /// <param name="rootDirectory">The directory the resource serves.</param>
            public TestResourceFile(string rootDirectory)
                : base(new ResourceContext { Route = new RouteEndpoint("/files") })
            {
                RootDirectory = rootDirectory;
            }
        }

        /// <summary>
        /// A file inside the served directory is delivered.
        /// </summary>
        [Fact]
        public void FileInsideRootIsServed()
        {
            // arrange
            using var directory = new TestDirectory();
            var resource = new TestResourceFile(directory.Served);
            var request = UnitTestFixture.CreateRequestMock(uri: "/files/public.txt");

            // act
            var response = resource.Process(request);

            // validation
            Assert.IsType<ResponseOK>(response);
        }

        /// <summary>
        /// The url is the client's to choose, so dot segments - with either separator, since
        /// windows reads a backslash as one - must not reach a file next to the served directory.
        /// A sibling sharing the directory's name as a prefix must stay out of reach as well.
        /// </summary>
        /// <param name="url">The requested url.</param>
        [Theory]
        [InlineData("/files/../secret.txt")]
        [InlineData("/files/..\\secret.txt")]
        [InlineData("/files/..\\served-sibling\\secret.txt")]
        [InlineData("/files-sibling/secret.txt")]
        public void FileOutsideRootIsNotServed(string url)
        {
            // arrange
            using var directory = new TestDirectory();
            var resource = new TestResourceFile(directory.Served);
            var request = UnitTestFixture.CreateRequestMock(uri: url);

            // act
            var response = resource.Process(request);

            // validation
            Assert.IsType<ResponseNotFound>(response);
        }

        /// <summary>
        /// A temporary layout of a served directory, a secret next to it and a sibling
        /// whose name starts with the served directory's name.
        /// </summary>
        private sealed class TestDirectory : IDisposable
        {
            /// <summary>
            /// Gets the temporary parent of the layout.
            /// </summary>
            public string Parent { get; } = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

            /// <summary>
            /// Gets the directory the resource serves.
            /// </summary>
            public string Served => Path.Combine(Parent, "served");

            /// <summary>
            /// Initializes a new instance of the class.
            /// </summary>
            public TestDirectory()
            {
                Directory.CreateDirectory(Served);
                Directory.CreateDirectory(Path.Combine(Parent, "served-sibling"));
                File.WriteAllText(Path.Combine(Served, "public.txt"), "public");
                File.WriteAllText(Path.Combine(Parent, "secret.txt"), "secret");
                File.WriteAllText(Path.Combine(Parent, "served-sibling", "secret.txt"), "secret");
            }

            /// <summary>
            /// Removes the layout.
            /// </summary>
            public void Dispose()
            {
                Directory.Delete(Parent, true);
            }
        }
    }
}
