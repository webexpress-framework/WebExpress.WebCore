using WebExpress.WebCore.Test.Fixture;
using WebExpress.WebCore.Test.WWW;
using WebExpress.WebCore.WebEndpoint;
using WebExpress.WebCore.WebUri;

namespace WebExpress.WebCore.Test.WebUri
{
    /// <summary>
    /// Tests an uri.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestUri
    {
        /// <summary>
        /// Test the constructor with absolute URIs.
        /// </summary>
        [Theory]
        [InlineData(UriScheme.Http, "www.example.com", null, null, null, "a=1&b=2", "fragment", "http://www.example.com/?a=1&b=2#fragment")]
        [InlineData(UriScheme.Http, "www.example.com", null, null, "", "a=1&b=2", "fragment", "http://www.example.com/?a=1&b=2#fragment")]
        [InlineData(UriScheme.Http, "www.example.com", null, null, "/abc", "a=1&b=2", "fragment", "http://www.example.com/abc?a=1&b=2#fragment")]
        [InlineData(UriScheme.Http, "example.com", "user", "8080", "/abc", "a=1&b=2", "fragment", "http://user@example.com:8080/abc?a=1&b=2#fragment")]
        [InlineData(UriScheme.Http, "example", null, null, "/assets/img/example.svg", null, null, "http://example/assets/img/example.svg")]
        [InlineData(UriScheme.Http, "localhost", null, null, null, null, null, "http://localhost/")]
        [InlineData(UriScheme.Http, "localhost", null, null, "/", null, null, "http://localhost/")]
        [InlineData(UriScheme.Http, "example.com", "user", "80", "/abc", "a=1&b=2", "fragment", "http://user@example.com/abc?a=1&b=2#fragment")]
        [InlineData(UriScheme.Http, "localhost", null, null, "/app/page", "q=~", null, "http://localhost/app/page?q=~")]
        public void UriAbsolute(UriScheme scheme, string authority, string user, string port, string path, string query, string fragment, string expected)
        {
            // arrange
            var uriUser = user is not null ? user + "@" : "";
            var uriPort = port is not null ? ":" + port : null;
            var uriQuery = query is not null ? "?" + query : "";
            var uriFragment = fragment is not null ? "#" + fragment : null;

            // act
            var uri = new UriEndpoint($"{scheme}://{uriUser}{authority}{uriPort}{path}{uriQuery}{uriFragment}");

            // validation
            Assert.Equal(expected, uri.ToString());
            Assert.Equal(scheme, uri.Scheme);
            Assert.Equal(authority, uri.Authority.Host);
            Assert.Equal(path, !string.IsNullOrWhiteSpace(path)
                ? "/" + string.Join("/", uri.PathSegments.Skip(1))
                : path);
            Assert.Equal(query, uri.Query.Any() ? string.Join("&", uri.Query) : null);
            Assert.Equal(fragment, uri.Fragment);
        }

        /// <summary>
        /// Test the constructor with relative URIs.
        /// </summary>
        [Theory]
        [InlineData(null, null, null, "/")]
        [InlineData(null, "a=1&b=2", null, "/?a=1&b=2")]
        [InlineData(null, null, "fragment", "/#fragment")]
        [InlineData(null, "a=1&b=2", "fragment", "/?a=1&b=2#fragment")]
        [InlineData("", null, null, "/")]
        [InlineData("", "a=1&b=2", null, "/?a=1&b=2")]
        [InlineData("", null, "fragment", "/#fragment")]
        [InlineData("", "a=1&b=2", "fragment", "/?a=1&b=2#fragment")]
        [InlineData("/", null, null, "/")]
        [InlineData("/", "a=1&b=2", null, "/?a=1&b=2")]
        [InlineData("/abc", "a=1&b=2", "fragment", "/abc?a=1&b=2#fragment")]
        [InlineData("/assets/img/example.svg", null, null, "/assets/img/example.svg")]
        [InlineData("/app/page", "q=~", null, "/app/page?q=~")]
        [InlineData("/~user/home", "a=1", null, "/~user/home?a=1")]
        [InlineData("/a", "filter=name~'x',b:c@d!;e", "top~", "/a?filter=name~'x',b:c@d!;e#top~")]
        [InlineData("/a", "json={\"k\":[1|2]}", null, "/a?json={\"k\":[1|2]}")]
        public void UriRelative(string path, string query, string fragment, string expected)
        {
            // arrange
            var uriQuery = query is not null ? "?" + query : "";
            var uriFragment = fragment is not null ? "#" + fragment : null;

            // act
            var uri = new UriEndpoint($"{path}{uriQuery}{uriFragment}");

            // validation
            Assert.Equal(expected, uri.ToString());
            Assert.Equal(path, !string.IsNullOrWhiteSpace(path)
                ? "/" + string.Join("/", uri.PathSegments.Skip(1))
                : path);
            Assert.Equal(query, uri.Query.Any() ? string.Join("&", uri.Query) : null);
            Assert.Equal(fragment, uri.Fragment);
        }

        /// <summary>
        /// Test the concat method.
        /// </summary>
        [Theory]
        [InlineData("/a/b/c", null, "/a/b/c", 4)]
        [InlineData("/a/b/c", "", "/a/b/c", 4)]
        [InlineData("/a/b/c", "d", "/a/b/c/d", 5)]
        [InlineData("/a/b/c", "/d/e/f", "/a/b/c/d/e/f", 7)]
        public void Concat(string path, string segment, string expected, int count)
        {
            // arrange
            var uri = new UriEndpoint(path);

            // act
            var concat = uri.Concat(segment);

            // validation
            Assert.Equal(expected, concat.ToString());
            Assert.Equal(count, concat.PathSegments.Count());
        }

        /// <summary>
        /// Test the concat method.
        /// </summary>
        [Theory]
        [InlineData(null, null, "/")]
        [InlineData("a", null, "/?a=")]
        [InlineData("b", "x", "/?b=x")]
        public void ConcatQuery(string key, string value, string expected)
        {
            // arrange
            var uri = new UriEndpoint();

            // act
            var concat = uri.Concat(key is not null ? new UriQuery(key, value) : null);

            // validation
            Assert.Equal(expected, concat.ToString());
        }

        /// <summary>
        /// Tests that adding a query leaves the shared uri untouched, so an endpoint extended
        /// once per row carries only that row's query.
        /// </summary>
        [Fact]
        public void AddQueryKeepsOriginal()
        {
            // arrange
            var uri = new UriEndpoint(new UriPathSegmentConstant("api"));

            // act
            var first = uri.Add(new UriQuery("id", "1"));
            var second = uri.Add(new UriQuery("id", "2"));

            // validation
            Assert.Equal("/api", uri.ToString());
            Assert.Equal("/api?id=1", first.ToString());
            Assert.Equal("/api?id=2", second.ToString());
        }

        /// <summary>
        /// Test the skip method.
        /// </summary>
        [Theory]
        [InlineData("/a/b/c", 0, "/a/b/c")]
        [InlineData("/a/b/c", 1, "/a/b/c")]
        [InlineData("/a/b/c", 2, "/b/c")]
        [InlineData("/a/b/c", 3, "/c")]
        [InlineData("/a/b/c", 4, null)]
        [InlineData("/a/b/c", 5, null)]
        public void Skip(string path, int skipCount, string expected)
        {
            // arrange
            var uri = new UriEndpoint(path);

            // act
            var skip = uri.Skip(skipCount);

            // validation
            Assert.Equal(expected, skip?.ToString());
        }

        /// <summary>
        /// Test the take method.
        /// </summary>
        [Theory]
        [InlineData("/a/b/c", 0, "/")]
        [InlineData("/a/b/c", 1, "/")]
        [InlineData("/a/b/c", 2, "/a")]
        [InlineData("/a/b/c", 3, "/a/b")]
        [InlineData("/a/b/c", 4, "/a/b/c")]
        [InlineData("/a/b/c", 5, "/a/b/c")]
        [InlineData("/a/b/c", -1, "/a/b")]
        [InlineData("/a/b/c", -2, "/a")]
        [InlineData("/a/b/c", -3, "/")]
        [InlineData("/a/b/c", -4, null)]
        [InlineData("/a/b/c", -5, null)]
        public void Take(string path, int takeCount, string expected)
        {
            // arrange
            var uri = new UriEndpoint(path);

            // act
            var take = uri.Take(takeCount);

            // validation
            Assert.Equal(expected, take?.ToString());
        }

        /// <summary>
        /// Test the take last method.
        /// </summary>
        [Theory]
        [InlineData("/a/b/c", 0, "/a/b/c")]
        [InlineData("/a/b/c", 1, "/c")]
        [InlineData("/a/b/c", 2, "/b/c")]
        [InlineData("/a/b/c", 3, "/a/b/c")]
        [InlineData("/a/b/c", 4, "/a/b/c")]
        [InlineData("/a/b/c", 5, "/a/b/c")]
        [InlineData("/a/b/c", -5, "/a/b/c")]
        public void TakeLast(string path, int takeCount, string expected)
        {
            // arrange
            var uri = new UriEndpoint(path);

            // act
            var take = uri.TakeLast(takeCount);

            // validation
            Assert.Equal(expected, take?.ToString());
        }

        /// <summary>
        /// Test the merge method.
        /// </summary>
        [Theory]
        [InlineData("http://www.example.com", "/a/b/c", "http://www.example.com/a/b/c")]
        [InlineData("http://www.example.com/", "/a/b/c", "http://www.example.com/a/b/c")]
        [InlineData("http://www.example.com/a/b/c", "/a/b/c", "http://www.example.com/a/b/c")]
        [InlineData("http://www.example.com/a/$guid/c", "/a/$guid/c", "http://www.example.com/a/$guid/c")]
        public void Merge(string uri, string route, string expected)
        {
            // arrange
            var random = Guid.NewGuid().ToString();
            var uriEndpoint = new UriEndpoint(uri.Replace("$guid", random));
            var routeEndpoint = new RouteEndpoint
            (
                [.. route.Split('/').Select
                (
                    x => (IUriPathSegment)(x == "$guid"
                        ? new UriPathSegmentVariableGuid<TestParameterA>("guid") { Value = random }
                        : new UriPathSegmentConstant(x))
                )]
            );

            // act
            var resourceUri = new UriEndpoint(uriEndpoint, routeEndpoint.PathSegments);

            // validation
            Assert.Equal(expected.Replace("$guid", random), resourceUri?.ToString());
        }

        /// <summary>
        /// Test the base path property.
        /// </summary>
        [Theory]
        [InlineData("http://user@example.com/x/y/z", "http://user@example.com/x", "http://user@example.com/x")]
        [InlineData("http://user@example.com/a/b/c/x/y/z", "http://user@example.com/a/b/c", "http://user@example.com/a/b/c")]
        public void BasePath(string uri, string baseUri, string expected)
        {
            // act
            var resourceUri = new UriEndpoint(uri)
            {
                BasePath = new UriEndpoint(baseUri)
            };

            // validation
            Assert.Equal(uri, resourceUri.ToString());
            Assert.Equal(expected, resourceUri.BasePath.ToString());
        }

        /// <summary>
        /// Test the SetFragment method.
        /// </summary>
        [Theory]
        [InlineData("http://user@example.com/x", null, "http://user@example.com/x")]
        [InlineData("http://user@example.com/x", "", "http://user@example.com/x")]
        [InlineData("http://user@example.com/x?a=1&b=2", "myfragment", "http://user@example.com/x?a=1&b=2#myfragment")]
        [InlineData("http://user@example.com/a/b/c", "myfragment", "http://user@example.com/a/b/c#myfragment")]
        public void SetFragment(string uri, string fragment, string expected)
        {
            // arrange
            var resourceUri = (IUri)new UriEndpoint(uri)
            {
            };

            // act
            resourceUri = resourceUri.SetFragment(fragment);

            // validation
            Assert.Equal(expected, resourceUri.ToString());
        }

        /// <summary>
        /// Test the GetDisplayText method.
        /// </summary>
        [Theory]
        [InlineData(typeof(TestApplicationA), typeof(WWW.Index), null)]
        [InlineData(typeof(TestApplicationA), typeof(About), null)]
        [InlineData(typeof(TestApplicationA), typeof(Contact), null)]
        [InlineData(typeof(TestApplicationB), typeof(WWW.Index), null)]
        [InlineData(typeof(TestApplicationB), typeof(About), null)]
        [InlineData(typeof(TestApplicationB), typeof(Contact), null)]
        [InlineData(typeof(TestApplicationC), typeof(WWW.Index), null)]
        [InlineData(typeof(TestApplicationC), typeof(About), null)]
        [InlineData(typeof(TestApplicationC), typeof(Contact), null)]
        public void GetDisplayText(Type applicationType, Type resourceType, string expected)
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();
            var application = componentHub.ApplicationManager.GetApplications(applicationType)?.FirstOrDefault();
            componentHub.SitemapManager.Refresh();
            var page = componentHub.PageManager.GetPages(resourceType, application)?.FirstOrDefault();
            var endpoint = componentHub.SitemapManager.GetEndpoint(page.Route.ToUri());
            var renderContext = UnitTestFixture.CrerateRenderContextMock(application);

            // act
            var display = endpoint.Route.ToUri().GetDisplayText(renderContext);

            // validation
            Assert.Equal(expected, display);
        }

        /// <summary>
        /// Tests the BindParameters method to verify that parameters are correctly 
        /// bound to the URI endpoint.
        /// </summary>
        [Fact]
        public void BindParametersSingle()
        {
            // arrange
            var pathSegments = new IUriPathSegment[]
            {
                new UriPathSegmentConstant("a"),
                new UriPathSegmentVariableGuid<TestParameterA>("testparametera"),
                new UriPathSegmentConstant("b")
            };

            var uri = new UriEndpoint(pathSegments);
            var parameter = new TestParameterA()
            {
                Value = "CFABEA8C-4223-4E17-AB31-C8FA4454B745"
            };

            // act
            var bind = uri.BindParameters(parameter);

            // validation
            Assert.Equal("/a/${testparametera}/b", uri.ToString());
            Assert.Equal("/a/CFABEA8C-4223-4E17-AB31-C8FA4454B745/b", bind.ToString());
        }

        /// <summary>
        /// Tests the BindParameters method to verify that parameters are correctly 
        /// bound to the URI endpoint.
        /// </summary>
        [Fact]
        public void BindParametersMultible()
        {
            // arrange
            var pathSegments = new IUriPathSegment[]
            {
                new UriPathSegmentConstant("a"),
                new UriPathSegmentVariableInt<TestParameterA>(),
                new UriPathSegmentConstant("b"),
                new UriPathSegmentVariableInt<TestParameterB>()
            };

            var uri = new UriEndpoint(pathSegments);
            var parameter1 = new TestParameterA()
            {
                Value = "10"
            };

            var parameter2 = new TestParameterB()
            {
                Value = "20"
            };

            // act
            var bind = uri.BindParameters(parameter1, parameter2);

            // validation
            Assert.Equal("/a/${testparametera}/b/${testparameterb}", uri.ToString());
            Assert.Equal("/a/10/b/20", bind.ToString());
        }

        /// <summary>
        /// Tests the BindParameters method to verify that parameters are correctly 
        /// bound to the URI endpoint.
        /// </summary>
        [Fact]
        public void BindParametersQuery()
        {
            // arrange
            var pathSegments = new IUriPathSegment[]
            {
                new UriPathSegmentConstant("a"),
                new UriPathSegmentVariableInt<TestParameterA>(),
                new UriPathSegmentConstant("b")
            };

            var uri = new UriEndpoint(pathSegments)
                .Add(new UriQuery<TestParameterB>());
            var parameter1 = new TestParameterA()
            {
                Value = "10"
            };

            var parameter2 = new TestParameterB()
            {
                Value = "20"
            };

            // act
            var bind = uri.BindParameters(parameter1, parameter2);

            // validation
            Assert.Equal("/a/${testparametera}/b?testparameterb=", uri.ToString());
            Assert.Equal("/a/10/b?testparameterb=20", bind.ToString());
        }
    }
}
