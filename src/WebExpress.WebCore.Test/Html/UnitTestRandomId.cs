using System.Collections.Concurrent;
using WebExpress.WebCore.WebHtml;

namespace WebExpress.WebCore.Test.Html
{
    /// <summary>
    /// Unit tests for the RandomId class.
    /// </summary>
    public class UnitTestRandomId
    {
        /// <summary>
        /// Tests the create method.
        /// </summary>
        [Fact]
        public void CreateDifferent()
        {
            // act
            var id1 = RandomId.Create();
            var id2 = RandomId.Create();

            // validation
            Assert.NotEqual(id1, id2);
        }

        /// <summary>
        /// Tests the create method.
        /// </summary>
        [Fact]
        public void Prefix()
        {
            // act
            var id = RandomId.Create();

            // validation
            Assert.StartsWith("id_", id);
        }

        /// <summary>
        /// Tests the create method.
        /// </summary>
        [Fact]
        public void Length()
        {
            // act
            var id = RandomId.Create();

            // validation
            Assert.Equal("id_".Length + 32, id.Length);
        }

        /// <summary>
        /// Tests the create method.
        /// </summary>
        [Fact]
        public void HexCharacters()
        {
            // act
            var id = RandomId.Create();

            // validation
            var hex = id.Substring("id_".Length);
            Assert.Matches("^[0-9A-F]+$", hex);
        }

        /// <summary>
        /// Tests the create method.
        /// </summary>
        [Fact]
        public void ThreadSafe()
        {
            // arrange
            var results = new ConcurrentBag<string>();

            Parallel.For(0, 5000, _ =>
            {
                // act
                results.Add(RandomId.Create());
            });

            // validation
            var distinct = results.Distinct().Count();

            Assert.Equal(results.Count, distinct);
        }

        /// <summary>
        /// Tests the create method.
        /// </summary>
        [Fact]
        public void NotNullOrEmpty()
        {
            // act
            var id = RandomId.Create();

            // validation
            Assert.False(string.IsNullOrWhiteSpace(id));
        }
    }
}
