namespace WebExpress.WebCore.Test
{
    /// <summary>
    /// Unit tests for the ArgumentParser class.
    /// </summary>
    public class UnitTestArgumentParser
    {
        /// <summary>
        /// Creates a parser with the commands of the host, independent of the shared singleton.
        /// </summary>
        private static ArgumentParser CreateParser()
        {
            var parser = new ArgumentParser();
            parser.Register(new ArgumentParserCommand() { FullName = "help", ShortName = "h" });
            parser.Register(new ArgumentParserCommand() { FullName = "config", ShortName = "c", ParameterDescription = "<file>" });

            return parser;
        }

        /// <summary>
        /// Tests that the short and the long form are stored under the full name of the command.
        /// </summary>
        [Fact]
        public void ShortAndLongForm()
        {
            // act
            var result = CreateParser().Parse(["-c", "a.xml", "-HELP"]);

            // validation
            Assert.Equal("a.xml", result["config"]);
            Assert.Equal("", result["help"]);
        }

        /// <summary>
        /// Tests that a repeated argument does not throw and the last occurrence wins.
        /// </summary>
        [Fact]
        public void RepeatedArgument()
        {
            // act
            var result = CreateParser().Parse(["-c", "a.xml", "-config", "b.xml"]);

            // validation
            Assert.Single(result);
            Assert.Equal("b.xml", result["config"]);
        }

        /// <summary>
        /// Tests that unknown arguments and comments are ignored.
        /// </summary>
        [Fact]
        public void UnknownAndComment()
        {
            // act
            var result = CreateParser().Parse(["-x", "1", "--note", "-h"]);

            // validation
            Assert.Equal(new[] { "help" }, result.Keys);
        }

        /// <summary>
        /// Tests that the overview separates each short form from its parameter description.
        /// </summary>
        [Fact]
        public void ToStringOverview()
        {
            // validation
            Assert.Equal("-h | -c <file>", CreateParser().ToString());
        }
    }
}
