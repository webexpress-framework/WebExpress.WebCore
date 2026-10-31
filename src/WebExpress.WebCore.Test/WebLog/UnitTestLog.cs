using WebExpress.WebCore.WebSetting;
using WebExpress.WebCore.WebLog;

namespace WebExpress.WebCore.Test.WebLog
{
    /// <summary>
    /// Unit tests for the Log class, covering the counters, the in-memory 
    /// recent-entry buffer, the EntryLogged event and the file lifecycle.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestLog
    {
        /// <summary>
        /// Creates a unique temporary directory for file-based tests.
        /// </summary>
        /// <returns>The path of the created directory.</returns>
        private static string CreateTempDirectory()
        {
            var dir = Path.Combine(Path.GetTempPath(), "wxlogtest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);

            return dir;
        }

        /// <summary>
        /// Tests that warnings, errors, fatal errors and exceptions update the counters and that
        /// errors aggregate exceptions.
        /// </summary>
        [Fact]
        public void CountersAreTracked()
        {
            // arrange
            var log = new Log { LogMode = LogMode.Off };

            // act
            log.Warning("w");
            log.Error("e");
            log.FatalError("f");
            log.Exception(new Exception("boom"));

            // validation
            Assert.Equal(1, log.WarningCount);
            Assert.Equal(1, log.ExceptionCount);
            // error + fatal error + exception all count as errors
            Assert.Equal(3, log.ErrorCount);
        }

        /// <summary>
        /// Tests that clearing resets all counters.
        /// </summary>
        [Fact]
        public void ClearResetsCounters()
        {
            // arrange
            var log = new Log { LogMode = LogMode.Off };
            log.Error("e");
            log.Warning("w");
            log.Exception(new Exception("boom"));

            // act
            log.Clear();

            // validation
            Assert.Equal(0, log.ErrorCount);
            Assert.Equal(0, log.WarningCount);
            Assert.Equal(0, log.ExceptionCount);
        }

        /// <summary>
        /// Tests that logged messages are retained in the in-memory buffer, even when no log file is
        /// written.
        /// </summary>
        [Fact]
        public void RecentEntriesAreRecorded()
        {
            // arrange
            var log = new Log { LogMode = LogMode.Off };

            // act
            log.Info("hello recent");
            var recent = log.GetRecentEntries();

            // validation
            var entry = recent.Last();
            Assert.Equal("hello recent", entry.Message);
            Assert.Equal(LogLevel.Info, entry.Level);
        }

        /// <summary>
        /// Tests that the recent buffer never grows beyond the configured capacity and keeps the
        /// newest entries.
        /// </summary>
        [Fact]
        public void RecentEntriesHonorCapacity()
        {
            // arrange
            var log = new Log { LogMode = LogMode.Off, RecentCapacity = 2 };

            // act
            for (var i = 0; i < 5; i++)
            {
                log.Info("m" + i);
            }
            var recent = log.GetRecentEntries();

            // validation
            Assert.Equal(2, recent.Count);
            Assert.Equal("m3", recent[0].Message);
            Assert.Equal("m4", recent[1].Message);
        }

        /// <summary>
        /// Tests that the EntryLogged event is raised for each recorded entry.
        /// </summary>
        [Fact]
        public void EntryLoggedEventIsRaised()
        {
            // arrange
            var log = new Log { LogMode = LogMode.Off };
            LogEntry received = null;
            log.EntryLogged += (s, e) => received = e;

            // act
            log.Info("event payload");

            // validation
            Assert.NotNull(received);
            Assert.Equal("event payload", received.Message);
        }

        /// <summary>
        /// Tests that closing a logger that was never started does not throw, even though no file
        /// path has been configured.
        /// </summary>
        [Fact]
        public void CloseWithoutBeginDoesNotThrow()
        {
            // arrange
            var log = new Log { LogMode = LogMode.Off };

            // act & validation
            var exception = Record.Exception(log.Close);
            Assert.Null(exception);
        }

        /// <summary>
        /// Tests the full append lifecycle: starting, writing and flushing on close produces a file
        /// that contains the message.
        /// </summary>
        [Fact]
        public void AppendWritesToFile()
        {
            // arrange
            var dir = CreateTempDirectory();
            try
            {
                var log = new Log { LogMode = LogMode.Append };

                // act
                log.Begin(dir, "test.log");
                log.Info("written to disk");
                log.Close();

                // validation
                var file = Path.Combine(dir, "test.log");
                Assert.True(File.Exists(file));
                Assert.Contains("written to disk", File.ReadAllText(file));
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        /// <summary>
        /// Tests that override mode discards an existing log file when logging starts.
        /// </summary>
        [Fact]
        public void OverrideDiscardsExistingFile()
        {
            // arrange
            var dir = CreateTempDirectory();
            var file = Path.Combine(dir, "test.log");
            File.WriteAllText(file, "PREVIOUS CONTENT");
            try
            {
                var log = new Log { LogMode = LogMode.Override };

                // act
                log.Begin(dir, "test.log");
                log.Info("fresh content");
                log.Close();

                // validation
                var content = File.ReadAllText(file);
                Assert.DoesNotContain("PREVIOUS CONTENT", content);
                Assert.Contains("fresh content", content);
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        /// <summary>
        /// Tests that a malformed log mode in the settings does not crash startup and falls back to
        /// the existing mode.
        /// </summary>
        [Fact]
        public void BeginWithInvalidModeFallsBack()
        {
            // arrange
            var dir = CreateTempDirectory();
            try
            {
                var log = new Log();
                var settings = new LogSettings
                {
                    Mode = "not-a-mode",
                    Debug = false,
                    Path = dir,
                    Encoding = "utf-8",
                    FileName = "settings.log",
                    TimePattern = "HH:mm:ss"
                };

                // act
                var exception = Record.Exception(() => log.Begin(settings));
                log.Close();

                // validation
                Assert.Null(exception);
                Assert.Equal(LogMode.Append, log.LogMode);
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        /// <summary>
        /// Tests that a valid log mode in the settings is parsed case-insensitively.
        /// </summary>
        [Fact]
        public void BeginParsesModeCaseInsensitive()
        {
            // arrange
            var dir = CreateTempDirectory();
            try
            {
                var log = new Log();
                var settings = new LogSettings
                {
                    Mode = "override",
                    Debug = false,
                    Path = dir,
                    Encoding = "utf-8",
                    FileName = "settings.log",
                    TimePattern = "HH:mm:ss"
                };

                // act
                log.Begin(settings);
                log.Close();

                // validation
                Assert.Equal(LogMode.Override, log.LogMode);
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }
    }
}
