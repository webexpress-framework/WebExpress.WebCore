using WebExpress.WebCore.WebHtml;

namespace WebExpress.WebCore.Test.Html
{
    /// <summary>
    /// Unit tests for the DeterministicId class.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestDeterministicId
    {
        /// <summary>
        /// Tests the create method.
        /// </summary>
        [Fact]
        public void CreateSameCallsite()
        {
            // act
            var id1 = CallCreate();
            var id2 = CallCreate();

            // validation
            Assert.NotEqual(id1, id2);
        }

        /// <summary>
        /// Tests the create method.
        /// </summary>
        [Fact]
        public void CreateIndexes()
        {
            // act
            var id1 = CallCreate(0);
            var id2 = CallCreate(1);

            // validation
            Assert.NotEqual(id1, id2);
        }

        /// <summary>
        /// Tests the create method.
        /// </summary>
        [Fact]
        public void CreateDifferentCallsites()
        {
            // arrange
            var CallsiteA = new Func<string>(() => CallCreate());
            var CallsiteB = new Func<string>(() => CallCreate());

            // act
            var id1 = CallsiteA();
            var id2 = CallsiteB();

            // validation
            Assert.NotEqual(id1, id2);
        }


        /// <summary>
        /// Tests that repeated calls from the same call site (as happens when controls are created
        /// in a loop) all yield distinct ids. The previous call-stack based implementation produced
        /// duplicates here because the stack is identical across iterations.
        /// </summary>
        [Fact]
        public void CreateInLoopAreUnique()
        {
            // act
            var ids = new HashSet<string>();
            for (var i = 0; i < 1000; i++)
            {
                ids.Add(DeterministicId.Create());
            }

            // validation
            Assert.Equal(1000, ids.Count);
        }

        /// <summary>
        /// Tests that two scopes - two requests for the same page - hand out the same ids, which
        /// is what lets a form without an explicit id recognize its own submit.
        /// </summary>
        [Fact]
        public void ScopeRepeatsIds()
        {
            // act
            var first = CreateInScope(3);
            var second = CreateInScope(3);

            // validation
            Assert.Equal(first, second);
            Assert.Equal(3, first.Distinct().Count());
        }

        /// <summary>
        /// Tests that an id created outside of a scope never matches one counted inside, as a
        /// control built at startup may be rendered next to the controls of a request.
        /// </summary>
        [Fact]
        public void ScopeDiffersFromUnscoped()
        {
            // act
            var unscoped = Enumerable.Range(0, 3).Select(_ => DeterministicId.Create()).ToList();
            var scoped = CreateInScope(3);

            // validation
            Assert.All(unscoped, x => Assert.StartsWith("id_g", x));
            Assert.Empty(unscoped.Intersect(scoped));
        }

        /// <summary>
        /// Tests that closing a nested scope continues the count of the enclosing one.
        /// </summary>
        [Fact]
        public void ScopeNested()
        {
            // act
            using var outer = DeterministicId.BeginScope();
            var before = DeterministicId.Create();
            CreateInScope(5);
            var after = DeterministicId.Create();

            // validation
            Assert.Equal("id_1", before);
            Assert.Equal("id_2", after);
        }

        /// <summary>
        /// Creates identifiers inside a scope of their own.
        /// </summary>
        /// <param name="count">The number of identifiers to create.</param>
        /// <returns>The identifiers in the order they were created.</returns>
        private static List<string> CreateInScope(int count)
        {
            using var scope = DeterministicId.BeginScope();

            return [.. Enumerable.Range(0, count).Select(_ => DeterministicId.Create())];
        }

        /// <summary>
        /// Generates a deterministic identifier.
        /// <returns>
        /// A string that represents the generated deterministic identifier.
        /// </returns>
        private string CallCreate()
        {
            return DeterministicId.Create();
        }

        /// <summary>
        /// Generates a deterministic identifier based on the specified index.
        /// </summary>
        /// <param name="contet">
        /// The optional content used to influence the generated identifier. If 
        /// null, a default identifier is created.
        /// </param>
        /// <returns>
        /// A string that represents the generated deterministic identifier.
        /// </returns>
        private string CallCreate(object contet = null)
        {
            return DeterministicId.Create(contet);
        }
    }
}
