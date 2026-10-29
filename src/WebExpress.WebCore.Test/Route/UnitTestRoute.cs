using WebExpress.WebCore.WebEndpoint;
using WebExpress.WebCore.WebUri;

namespace WebExpress.WebCore.Test.Route
{
    /// <summary>
    /// Tests the route.
    /// </summary>
    public class UnitTestRoute
    {
        /// <summary>
        /// Test the concat method.
        /// </summary>
        [Theory]
        [InlineData("/a/b/c", null, "/a/b/c", 4)]
        [InlineData("/a/b/c", "", "/a/b/c", 4)]
        [InlineData("/a/b/c", "d", "/a/b/c/d", 5)]
        [InlineData("/a/b/c", "/d/e/f", "/a/b/c/d/e/f", 7)]
        public void ConcatString(string baseRoute, string segment, string expected, int count)
        {
            // arrange
            var route = new RouteEndpoint(baseRoute);

            // act
            var concat = route.Concat(segment);

            // validation
            Assert.Equal(expected, concat.ToString());
            Assert.Equal(count, concat.PathSegments.Count());
        }

        /// <summary>
        /// Test the concat method.
        /// </summary>
        [Theory]
        [InlineData("/a/b/c", null, "/a/b/c", 4)]
        [InlineData("/a/b/c", " ", "/a/b/c", 4)]
        [InlineData("/a/b/c", "d", "/a/b/c/d", 5)]
        [InlineData("/a/b/c", "/d/e/f", "/a/b/c/d/e/f", 7)]
        public void ConcatSegment(string baseRoute, string segment, string expected, int count)
        {
            // arrange
            var route = new RouteEndpoint(baseRoute);

            // act
            var concat = route.Concat(segment is not null
                ? [.. segment?.Split('/').Select(x => new UriPathSegmentConstant(x))]
                : null);

            // validation
            Assert.Equal(expected, concat.ToString());
            Assert.Equal(count, concat.PathSegments.Count());
        }

        /// <summary>
        /// Test the combine method.
        /// </summary>
        [Theory]
        [InlineData("/a/b/c", null, "/a/b/c")]
        [InlineData("/a/b/c", "", "/a/b/c")]
        [InlineData("/a/b/c", "d", "/a/b/c/d")]
        [InlineData("/a/b/c", "/d/e/f", "/a/b/c/d/e/f")]
        public void CombinePath(string baseRoute, string pathB, string expected)
        {
            // act
            var combine = RouteEndpoint.Combine([new RouteEndpoint(baseRoute), new RouteEndpoint(pathB)]);

            // validation
            Assert.Equal(expected, combine.ToString());
        }

        /// <summary>
        /// Test the combine method.
        /// </summary>
        [Theory]
        [InlineData("/a/b/c", null, "/a/b/c")]
        [InlineData("/a/b/c", "", "/a/b/c")]
        [InlineData("/a/b/c", "d", "/a/b/c/d")]
        [InlineData("/a/b/c", "/d/e/f", "/a/b/c/d/e/f")]
        public void CombineRoute(string baseRoute, string pathB, string expected)
        {
            // act
            var combine = RouteEndpoint.Combine(new RouteEndpoint(baseRoute), [pathB]);

            // validation
            Assert.Equal(expected, combine.ToString());
        }

        /// <summary>
        /// Test the combine method.
        /// </summary>
        [Theory]
        [InlineData("/a/b/c", null, "/a/b/c")]
        [InlineData("/a/b/c", "", "/a/b/c")]
        [InlineData("/a/b/c", "d", "/a/b/c/d")]
        [InlineData("/a/b/c", "/d/e/f", "/a/b/c/d/e/f")]
        public void CombineSegment(string baseRoute, string segment, string expected)
        {
            // act
            var combine = RouteEndpoint.Combine(new RouteEndpoint(baseRoute), segment);

            // validation
            Assert.Equal(expected, combine.ToString());
        }

        /// <summary>
        /// Test the combine method.
        /// </summary>
        [Theory]
        [InlineData("/a/b/c", null, "/a/b/c")]
        [InlineData("/a/b/c", "", "/a/b/c")]
        [InlineData("/a/b/c", "b", "/a/c")]
        [InlineData("/a/b/c", "/b", "/a/c")]
        [InlineData("/a/b/c", "/b/c", "/a")]
        [InlineData("/a/b/c", "/a/c", "/a/b/c")]
        public void RemoveSegment(string route, string segment, string expected)
        {
            // arrange
            var routeEndpoint = new RouteEndpoint(route);

            // act
            var removed = routeEndpoint.RemoveSegment(segment);

            // validation
            Assert.Equal(expected, removed.ToString());
        }
    }
}
