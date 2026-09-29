using Microsoft.Extensions.Configuration;
using WebExpress.WebCore.WebSetting;

namespace WebExpress.WebCore.Test.WebSetting
{
    /// <summary>
    /// Unit tests for the settings directory: how the files of the directory are merged into one
    /// configuration, which file wins, and how a plugin sees its own section.
    /// </summary>
    public class UnitTestSettingsDirectory : IDisposable
    {
        private readonly string _directory = Path.Combine(Environment.CurrentDirectory, Guid.NewGuid().ToString());

        /// <summary>
        /// Initializes a new instance of the class with an empty settings directory.
        /// </summary>
        public UnitTestSettingsDirectory()
        {
            Directory.CreateDirectory(_directory);
        }

        /// <summary>
        /// Removes the settings directory.
        /// </summary>
        public void Dispose()
        {
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, true);
            }
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Writes a file into the settings directory.
        /// </summary>
        /// <param name="name">The file name.</param>
        /// <param name="json">The content.</param>
        private void Write(string name, string json)
        {
            File.WriteAllText(Path.Combine(_directory, name), json);
        }

        /// <summary>
        /// Builds the configuration of the directory without a file watcher, so a test never
        /// races the watcher's delayed reload.
        /// </summary>
        /// <returns>The configuration.</returns>
        private IConfigurationRoot Build()
        {
            return new ConfigurationBuilder()
                .AddSettingsDirectory(_directory, reloadOnChange: false)
                .Build();
        }

        /// <summary>
        /// Tests that the values of every file of the directory end up in one configuration,
        /// each plugin under its own section.
        /// </summary>
        [Fact]
        public void AllFilesAreMerged()
        {
            // arrange
            Write("webexpress.settings.json", """{ "WebExpress": { "Culture": "en-US" } }""");
            Write("plugin.a.settings.json", """{ "Plugins": { "plugin.a": { "Greeting": "hello" } } }""");
            Write("plugin.b.settings.json", """{ "Plugins": { "plugin.b": { "Greeting": "hallo" } } }""");

            // act
            var configuration = Build();

            // validation
            Assert.Equal("en-US", configuration.GetServerSettings().Culture);
            Assert.Equal("hello", configuration.GetPluginSettings("plugin.a")["Greeting"]);
            Assert.Equal("hallo", configuration.GetPluginSettings("plugin.b")["Greeting"]);
        }

        /// <summary>
        /// Tests that a plugin only sees its own section, so nothing of the server or of
        /// another plugin leaks into it.
        /// </summary>
        [Fact]
        public void PluginSectionIsIsolated()
        {
            // arrange
            Write("webexpress.settings.json", """{ "WebExpress": { "Culture": "en-US" } }""");
            Write("plugin.a.settings.json", """{ "Plugins": { "plugin.a": { "Greeting": "hello" } } }""");

            // act
            var section = Build().GetPluginSettings("plugin.b");

            // validation
            Assert.False(section.Exists());
            Assert.Null(section["Greeting"]);
            Assert.Null(section["Culture"]);
        }

        /// <summary>
        /// Tests that the main file is merged last: a value the administrator puts there wins
        /// over the default a plugin ships in its own file, whatever the file names sort to.
        /// </summary>
        [Fact]
        public void MainFileOverridesPluginFile()
        {
            // arrange - "a..." sorts before "webexpress..." and "z..." after it
            Write("a.settings.json", """{ "Plugins": { "plugin.a": { "Greeting": "from a" } } }""");
            Write("z.settings.json", """{ "Plugins": { "plugin.z": { "Greeting": "from z" } } }""");
            Write("webexpress.settings.json", """{ "Plugins": { "plugin.a": { "Greeting": "from main" }, "plugin.z": { "Greeting": "from main" } } }""");

            // act
            var configuration = Build();

            // validation
            Assert.Equal("from main", configuration.GetPluginSettings("plugin.a")["Greeting"]);
            Assert.Equal("from main", configuration.GetPluginSettings("plugin.z")["Greeting"]);
        }

        /// <summary>
        /// Tests that among the other files the later name wins, so precedence can be read off
        /// a directory listing.
        /// </summary>
        [Fact]
        public void LaterFileNameOverridesEarlier()
        {
            // arrange
            Write("a.json", """{ "Plugins": { "plugin": { "Greeting": "from a" } } }""");
            Write("b.json", """{ "Plugins": { "plugin": { "Greeting": "from b" } } }""");

            // act
            var configuration = Build();

            // validation
            Assert.Equal("from b", configuration.GetPluginSettings("plugin")["Greeting"]);
        }

        /// <summary>
        /// Tests that files which are not json are left alone, so a readme or a backup in the
        /// directory does not break the start-up.
        /// </summary>
        [Fact]
        public void OtherFilesAreIgnored()
        {
            // arrange
            Write("webexpress.settings.json", """{ "WebExpress": { "Culture": "en-US" } }""");
            Write("readme.txt", "not json at all");
            Write("webexpress.settings.json.bak", "{ broken");

            // act
            var configuration = Build();

            // validation
            Assert.Equal("en-US", configuration.GetServerSettings().Culture);
        }

        /// <summary>
        /// Tests that a broken file fails the load with the file named, rather than being
        /// skipped silently.
        /// </summary>
        [Fact]
        public void BrokenFileFailsLoad()
        {
            // arrange
            Write("webexpress.settings.json", """{ "WebExpress": { "Culture": "en-US" } }""");
            Write("plugin.settings.json", "{ this is not json");

            // act
            var exception = Record.Exception(Build);

            // validation
            Assert.NotNull(exception);
            Assert.Contains("plugin.settings.json", exception.Message);
        }

        /// <summary>
        /// Tests that a reload picks up a file that did not exist when the configuration was
        /// built, which is how a package installed at runtime gets its settings in.
        /// </summary>
        [Fact]
        public void ReloadPicksUpNewFile()
        {
            // arrange
            Write("webexpress.settings.json", """{ "WebExpress": { "Culture": "en-US" } }""");
            var configuration = Build();
            var section = configuration.GetPluginSettings("plugin.new");
            Assert.False(section.Exists());

            // act
            Write("plugin.new.settings.json", """{ "Plugins": { "plugin.new": { "Greeting": "hello" } } }""");
            configuration.Reload();

            // validation - the section handed out before the reload sees the new value
            Assert.Equal("hello", section["Greeting"]);
        }

        /// <summary>
        /// Tests that a missing directory yields an empty configuration instead of an error, so
        /// a server without any settings file still binds its defaults.
        /// </summary>
        [Fact]
        public void MissingDirectoryIsEmpty()
        {
            // act
            var configuration = new ConfigurationBuilder()
                .AddSettingsDirectory(Path.Combine(_directory, "missing"), reloadOnChange: false)
                .Build();

            // validation
            Assert.Empty(configuration.AsEnumerable());
        }

        /// <summary>
        /// Tests that the environment overrides every file, so a container can change a value
        /// without touching a file.
        /// </summary>
        [Fact]
        public void EnvironmentOverridesFiles()
        {
            // arrange
            var variable = SettingsLoader.EnvironmentVariablePrefix + "WebExpress__Culture";
            Write("webexpress.settings.json", """{ "WebExpress": { "Culture": "en-US" } }""");
            Environment.SetEnvironmentVariable(variable, "de-DE");

            try
            {
                // act
                var configuration = SettingsLoader.Load(Path.Combine(_directory, "webexpress.settings.json"));
                using var configurationLifetime = configuration as IDisposable;

                // validation
                Assert.Equal("de-DE", configuration.GetServerSettings().Culture);
            }
            finally
            {
                Environment.SetEnvironmentVariable(variable, null);
            }
        }

        /// <summary>
        /// Verifies that repeated file changes keep renewing the watcher subscription.
        /// </summary>
        [Fact]
        public async System.Threading.Tasks.Task WatchedFilesReloadRepeatedly()
        {
            // arrange
            Write("webexpress.settings.json", "{ \"Value\": \"initial\" }");
            using var provider = new SettingsDirectoryConfigurationProvider(new()
            {
                Path = _directory,
                ReloadOnChange = true,
                ReloadDelay = 20
            });
            provider.Load();

            foreach (var value in new[] { "first", "second" })
            {
                var reloaded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                using var subscription = provider.GetReloadToken().RegisterChangeCallback(_ => reloaded.TrySetResult(), null);

                // act
                Write("webexpress.settings.json", $$"""{ "Value": "{{value}}" }""");
                await reloaded.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

                // validation
                Assert.True(provider.TryGet("Value", out var actual));
                Assert.Equal(value, actual);
            }
        }

        /// <summary>
        /// Verifies that disposal prevents a delayed watcher callback from publishing changes.
        /// </summary>
        [Fact]
        public async System.Threading.Tasks.Task DisposedWatcherDoesNotReload()
        {
            // arrange
            Write("webexpress.settings.json", "{}");
            using var provider = new SettingsDirectoryConfigurationProvider(new()
            {
                Path = _directory,
                ReloadOnChange = true,
                ReloadDelay = 300
            });
            provider.Load();
            var reloadToken = provider.GetReloadToken();

            // act
            Write("webexpress.settings.json", "{ \"Value\": \"changed\" }");
            await System.Threading.Tasks.Task.Delay(100, TestContext.Current.CancellationToken);
            provider.Dispose();
            Directory.Delete(_directory, true);
            await System.Threading.Tasks.Task.Delay(500, TestContext.Current.CancellationToken);

            // validation
            Assert.False(reloadToken.HasChanged);
            Assert.False(provider.TryGet("Value", out _));
        }

        /// <summary>
        /// Verifies that removing a watched directory does not leak failures from token renewal.
        /// </summary>
        [Fact]
        public async System.Threading.Tasks.Task RemovedWatchedDirectoryDoesNotLeakTaskExceptions()
        {
            // arrange
            Write("webexpress.settings.json", "{}");
            using var provider = new SettingsDirectoryConfigurationProvider(new()
            {
                Path = _directory,
                ReloadOnChange = true,
                ReloadDelay = 20
            });
            provider.Load();
            var errors = new System.Collections.Concurrent.ConcurrentQueue<Exception>();
            EventHandler<UnobservedTaskExceptionEventArgs> onUnobserved = (_, args) =>
            {
                if (args.Exception.ToString().Contains(_directory, StringComparison.OrdinalIgnoreCase))
                {
                    errors.Enqueue(args.Exception);
                    args.SetObserved();
                }
            };
            TaskScheduler.UnobservedTaskException += onUnobserved;
            try
            {
                // act
                Directory.Delete(_directory, true);
                for (var attempt = 0; attempt < 5; attempt++)
                {
                    await System.Threading.Tasks.Task.Delay(200, TestContext.Current.CancellationToken);
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                }

                // validation
                Assert.Empty(errors);
            }
            finally
            {
                TaskScheduler.UnobservedTaskException -= onUnobserved;
            }
        }

        /// <summary>
        /// Tests that the files are listed in the order they are merged, with the main file
        /// last, so the start-up log tells the administrator which file wins.
        /// </summary>
        [Fact]
        public void FilesAreListedInMergeOrder()
        {
            // arrange
            Write("webexpress.settings.json", "{}");
            Write("b.json", "{}");
            Write("a.json", "{}");
            var configuration = Build();

            // act
            var files = configuration.Providers
                .OfType<SettingsDirectoryConfigurationProvider>()
                .SelectMany(x => x.EnumerateFiles())
                .Select(x => Path.GetFileName(x))
                .ToList();

            // validation
            Assert.Equal(["a.json", "b.json", "webexpress.settings.json"], files);
        }
    }
}
