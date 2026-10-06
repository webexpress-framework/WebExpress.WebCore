namespace WebExpress.WebCore.Test.Server
{
    /// <summary>
    /// Unit tests for the container detection, fed with synthetic markers so the result does not
    /// depend on where the tests run.
    /// </summary>
    public class UnitTestContainerEnvironment
    {
        /// <summary>
        /// Runs the detection against the given environment variables and files.
        /// </summary>
        /// <param name="variables">The environment variables that are set.</param>
        /// <param name="files">The files that exist, with their content.</param>
        /// <returns>The detection result.</returns>
        private static bool Detect(Dictionary<string, string> variables = null, Dictionary<string, string> files = null)
        {
            variables ??= [];
            files ??= [];

            return ContainerEnvironment.Detect
            (
                name => variables.GetValueOrDefault(name),
                files.ContainsKey,
                path => files[path]
            );
        }

        /// <summary>
        /// Tests that a host without any marker is not taken for a container.
        /// </summary>
        [Fact]
        public void NoMarker()
        {
            Assert.False(Detect());
        }

        /// <summary>
        /// Tests that the environment variables set by the .NET images and by podman are recognized.
        /// </summary>
        [Theory]
        [InlineData("DOTNET_RUNNING_IN_CONTAINER", "true", true)]
        [InlineData("DOTNET_RUNNING_IN_CONTAINER", "True", true)]
        [InlineData("DOTNET_RUNNING_IN_CONTAINER", "false", false)]
        [InlineData("container", "podman", true)]
        [InlineData("container", "", false)]
        public void EnvironmentVariable(string name, string value, bool expected)
        {
            Assert.Equal(expected, Detect(variables: new() { [name] = value }));
        }

        /// <summary>
        /// Tests that the marker files of docker and podman are recognized.
        /// </summary>
        [Theory]
        [InlineData("/.dockerenv")]
        [InlineData("/run/.containerenv")]
        public void MarkerFile(string path)
        {
            Assert.True(Detect(files: new() { [path] = "" }));
        }

        /// <summary>
        /// Tests that the control groups of process 1 reveal a container under cgroup v1, while the
        /// anonymous cgroup v2 entry and a plain host stay unrecognized.
        /// </summary>
        [Theory]
        [InlineData("12:memory:/docker/3f2a9c\n", true)]
        [InlineData("11:cpu:/kubepods/besteffort/pod1\n", true)]
        [InlineData("0::/\n", false)]
        [InlineData("12:memory:/init.scope\n", false)]
        public void ControlGroup(string content, bool expected)
        {
            Assert.Equal(expected, Detect(files: new() { ["/proc/1/cgroup"] = content }));
        }

        /// <summary>
        /// Tests that an unreadable control group file counts as no container rather than failing.
        /// </summary>
        [Fact]
        public void UnreadableControlGroup()
        {
            var result = ContainerEnvironment.Detect
            (
                _ => null,
                path => path == "/proc/1/cgroup",
                _ => throw new UnauthorizedAccessException()
            );

            Assert.False(result);
        }
    }
}
