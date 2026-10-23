using System;
using System.IO;

namespace WebExpress.WebCore
{
    /// <summary>
    /// Tells whether the server runs inside a container (Docker, Podman, Kubernetes). Inside a
    /// container the console is the log: the runtime collects it (<c>docker logs</c>,
    /// <c>kubectl logs</c>), while a log file ends up in the container's ephemeral file system
    /// and is lost with the container. Features that manage the log file therefore adapt to it.
    /// </summary>
    public static class ContainerEnvironment
    {
        private static readonly Lazy<bool> _isContainer = new(() => Detect
        (
            Environment.GetEnvironmentVariable,
            path => OperatingSystem.IsLinux() && File.Exists(path),
            File.ReadAllText
        ));

        /// <summary>
        /// Determines whether the current process runs inside a container. The answer is probed
        /// once, since a process never moves in or out of a container.
        /// </summary>
        public static bool IsContainer => _isContainer.Value;

        /// <summary>
        /// Probes the markers container runtimes leave behind. No single marker is reliable on
        /// its own: the .NET images set an environment variable, Docker and Podman each place a
        /// file in the root, and older Docker hosts only show in the control groups of process 1.
        /// </summary>
        /// <param name="environmentVariable">Reads an environment variable, returning null when it is not set.</param>
        /// <param name="fileExists">Determines whether a file exists.</param>
        /// <param name="readFile">Reads the content of a file.</param>
        /// <returns>True when at least one marker points to a container, false otherwise.</returns>
        public static bool Detect
        (
            Func<string, string> environmentVariable,
            Func<string, bool> fileExists,
            Func<string, string> readFile
        )
        {
            // set by the official .NET images, the only marker windows containers offer
            if (string.Equals(environmentVariable("DOTNET_RUNNING_IN_CONTAINER"), "true", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // podman and systemd-nspawn announce themselves through this variable
            if (!string.IsNullOrWhiteSpace(environmentVariable("container")))
            {
                return true;
            }

            if (fileExists("/.dockerenv") || fileExists("/run/.containerenv"))
            {
                return true;
            }

            if (!fileExists("/proc/1/cgroup"))
            {
                return false;
            }

            try
            {
                // only cgroup v1 names the runtime here; under v2 the file reads "0::/"
                var cgroup = readFile("/proc/1/cgroup");

                return cgroup.Contains("docker", StringComparison.Ordinal)
                    || cgroup.Contains("kubepods", StringComparison.Ordinal);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return false;
            }
        }
    }
}
