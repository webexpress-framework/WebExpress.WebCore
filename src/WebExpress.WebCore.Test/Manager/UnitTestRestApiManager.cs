using WebExpress.WebCore.Test.Fixture;
using WebExpress.WebCore.Test.WWW.Api._1_;
using WebExpress.WebCore.Test.WWW.Api._2;
using WebExpress.WebCore.Test.WWW.Api.V3;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebParameter;
using WebExpress.WebCore.WebRestApi;

namespace WebExpress.WebCore.Test.Manager
{
    /// <summary>
    /// Test the rest api manager.
    /// </summary>
    public class UnitTestRestApiManager
    {
        /// <summary>
        /// Test the register function of the rest api manager.
        /// </summary>
        [Fact]
        public void Register()
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();

            // act
            Assert.Equal(9, componentHub.RestApiManager.RestApis.Count());
        }

        /// <summary>
        /// Test the remove function of the rest api manager.
        /// </summary>
        [Fact]
        public void Remove()
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();
            var plugin = componentHub.PluginManager?.GetPlugin(typeof(TestPlugin));
            var apiManager = componentHub.RestApiManager as RestApiManager;

            // act
            apiManager.Remove(plugin);

            // validation
            Assert.Empty(componentHub.RestApiManager.RestApis);
        }

        /// <summary>
        /// Test the id property of the rest api.
        /// </summary>
        [Theory]
        [InlineData(typeof(TestApplicationA), typeof(TestRestApiA), "webexpress.webcore.test.www.api._1_.testrestapia")]
        [InlineData(typeof(TestApplicationA), typeof(TestRestApiB), "webexpress.webcore.test.www.api._2.testrestapib")]
        [InlineData(typeof(TestApplicationA), typeof(TestRestApiC), "webexpress.webcore.test.www.api.v3.testrestapic")]
        [InlineData(typeof(TestApplicationB), typeof(TestRestApiA), "webexpress.webcore.test.www.api._1_.testrestapia")]
        [InlineData(typeof(TestApplicationB), typeof(TestRestApiB), "webexpress.webcore.test.www.api._2.testrestapib")]
        [InlineData(typeof(TestApplicationB), typeof(TestRestApiC), "webexpress.webcore.test.www.api.v3.testrestapic")]
        [InlineData(typeof(TestApplicationC), typeof(TestRestApiA), "webexpress.webcore.test.www.api._1_.testrestapia")]
        [InlineData(typeof(TestApplicationC), typeof(TestRestApiB), "webexpress.webcore.test.www.api._2.testrestapib")]
        [InlineData(typeof(TestApplicationC), typeof(TestRestApiC), "webexpress.webcore.test.www.api.v3.testrestapic")]
        public void Id(Type applicationType, Type resourceType, string id)
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();
            var application = componentHub.ApplicationManager.GetApplications(applicationType)?.FirstOrDefault();
            var api = componentHub.RestApiManager.GetRestApi(resourceType, application)?.FirstOrDefault();

            // act
            Assert.Equal(id, api?.EndpointId.ToString());
        }

        /// <summary>
        /// Test the context path property of the rest api.
        /// </summary>
        [Theory]
        [InlineData(typeof(TestApplicationA), typeof(TestRestApiA), "/server/appa/api/1/testrestapia")]
        [InlineData(typeof(TestApplicationA), typeof(TestRestApiB), "/server/appa/api/2/testrestapib")]
        [InlineData(typeof(TestApplicationA), typeof(TestRestApiC), "/server/appa/api/3/testrestapic")]
        [InlineData(typeof(TestApplicationB), typeof(TestRestApiA), "/server/appb/api/1/testrestapia")]
        [InlineData(typeof(TestApplicationB), typeof(TestRestApiB), "/server/appb/api/2/testrestapib")]
        [InlineData(typeof(TestApplicationB), typeof(TestRestApiC), "/server/appb/api/3/testrestapic")]
        [InlineData(typeof(TestApplicationC), typeof(TestRestApiA), "/server/api/1/testrestapia")]
        [InlineData(typeof(TestApplicationC), typeof(TestRestApiB), "/server/api/2/testrestapib")]
        [InlineData(typeof(TestApplicationC), typeof(TestRestApiC), "/server/api/3/testrestapic")]
        public void RoutePath(Type applicationType, Type resourceType, string path)
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();
            var application = componentHub.ApplicationManager.GetApplications(applicationType)?.FirstOrDefault();
            var api = componentHub.RestApiManager.GetRestApi(resourceType, application)?.FirstOrDefault();

            // act
            Assert.Equal(path, api?.Route.ToString());
        }

        /// <summary>
        /// Test the version from path property of the rest api.
        /// </summary>
        [Theory]
        [InlineData(typeof(TestApplicationA), typeof(TestRestApiA), "1")]
        [InlineData(typeof(TestApplicationA), typeof(TestRestApiB), "2")]
        [InlineData(typeof(TestApplicationA), typeof(TestRestApiC), "3")]
        [InlineData(typeof(TestApplicationB), typeof(TestRestApiA), "1")]
        [InlineData(typeof(TestApplicationB), typeof(TestRestApiB), "2")]
        [InlineData(typeof(TestApplicationB), typeof(TestRestApiC), "3")]
        [InlineData(typeof(TestApplicationC), typeof(TestRestApiA), "1")]
        [InlineData(typeof(TestApplicationC), typeof(TestRestApiB), "2")]
        [InlineData(typeof(TestApplicationC), typeof(TestRestApiC), "3")]
        public void Version(Type applicationType, Type resourceType, string expected)
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();
            var application = componentHub.ApplicationManager.GetApplications(applicationType)?.FirstOrDefault();
            var api = componentHub.RestApiManager.GetRestApi(resourceType, application)?.FirstOrDefault();
            componentHub.SitemapManager.Refresh();
            var uri = componentHub.SitemapManager.GetUri(resourceType, application);

            // act
            var version = uri.Parameters
                .Where(x => x.Key == "_apiversion")
                .FirstOrDefault();

            // act
            Assert.Equal(expected, version.Value);
        }

        /// <summary>
        /// Test the context path property of the rest api.
        /// </summary>
        [Theory]
        [InlineData(typeof(TestApplicationA), typeof(TestRestApiA), RequestMethod.POST)]
        [InlineData(typeof(TestApplicationA), typeof(TestRestApiA), RequestMethod.GET)]
        [InlineData(typeof(TestApplicationA), typeof(TestRestApiB), RequestMethod.GET)]
        public void Method(Type applicationType, Type resourceType, RequestMethod method)
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();
            var application = componentHub.ApplicationManager.GetApplications(applicationType)?.FirstOrDefault();
            var api = componentHub.RestApiManager.GetRestApi(resourceType, application)?.FirstOrDefault();

            // act
            Assert.Contains(method, api?.Methods);
        }

        /// <summary>
        /// Tests whether the rest api manager implements interface IComponentManager.
        /// </summary>
        [Fact]
        public void IsIComponentManager()
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();

            // act
            Assert.True(typeof(IComponentManager).IsAssignableFrom(componentHub.RestApiManager.GetType()));
        }

        /// <summary>
        /// Tests whether the rest api context implements interface IContext.
        /// </summary>
        [Fact]
        public void IsIContext()
        {
            // arrange
            var componentHub = UnitTestFixture.CreateAndRegisterComponentHubMock();

            // act
            foreach (var api in componentHub.RestApiManager.RestApis)
            {
                Assert.True(typeof(IContext).IsAssignableFrom(api.GetType()), $"Api context {api.GetType().Name} does not implement IContext.");
            }
        }

        /// <summary>
        /// Verifies that Require adds a validation error for missing or empty values.
        /// </summary>
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void ValidateRequire(string input)
        {
            // arrange
            var request = UnitTestFixture.CreateRequestMock($"name={input}");
            request.AddParameter(new Parameter("name", input, ParameterScope.Parameter));

            // act
            var validator = new RestApiValidator(request)
                .Require("name");

            // validation
            Assert.False(validator.IsValid);
            Assert.Contains(validator.Result.Errors, e => e.Field == "name" && e.Code == "REQUIRED");
        }

        /// <summary>
        /// Verifies that MinLength fails when input is shorter than allowed.
        /// </summary>
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("ab")]
        public void ValidateMinLength(string input)
        {
            // arrange
            var request = UnitTestFixture.CreateRequestMock();
            request.AddParameter(new Parameter("code", input, ParameterScope.Parameter));

            // act
            var validator = new RestApiValidator(request)
                .MinLength("code", 3);

            // validation
            Assert.False(validator.IsValid);
        }

        /// <summary>
        /// Verifies that MaxLength fails when input exceeds the specified limit.
        /// </summary>
        [Theory]
        [InlineData(256)]
        [InlineData(300)]
        public void ValidateMaxLength(int length)
        {
            // arrange
            var input = new string('x', length);
            var request = UnitTestFixture.CreateRequestMock();
            request.AddParameter(new Parameter("bio", input, ParameterScope.Parameter));

            // act
            var validator = new RestApiValidator(request)
                .MaxLength("bio", 255);

            // validation
            Assert.False(validator.IsValid);
            Assert.Contains(validator.Result.Errors, e => e.Field == "bio" && e.Code == "TOO_LONG");
        }

        /// <summary>
        /// Verifies that Email validation detects invalid email addresses.
        /// </summary>
        [Theory]
        [InlineData("invalid")]
        [InlineData("missing@domain")]
        [InlineData("@nouser.com")]
        public void ValidateEmail(string email)
        {
            // arrange
            var request = UnitTestFixture.CreateRequestMock();
            request.AddParameter(new Parameter("email", email, ParameterScope.Parameter));

            // act
            var validator = new RestApiValidator(request)
                .Email("email");

            // validation
            Assert.False(validator.IsValid);
            Assert.Contains(validator.Result.Errors, e => e.Code == "INVALID_EMAIL");
        }

        /// <summary>
        /// Verifies that IsInt fails when value is not a valid integer.
        /// </summary>
        [Theory]
        [InlineData("abc")]
        [InlineData("12.34")]
        [InlineData("123a")]
        public void ValidateIsInt(string input)
        {
            // arrange
            var request = UnitTestFixture.CreateRequestMock();
            request.AddParameter(new Parameter("age", input, ParameterScope.Parameter));

            // act
            var validator = new RestApiValidator(request)
                .IsInt("age");

            // validation
            Assert.False(validator.IsValid);
            Assert.Contains(validator.Result.Errors, e => e.Code == "NOT_INTEGER");
        }

        /// <summary>
        /// Verifies that EqualTo fails when value does not match expected.
        /// </summary>
        [Theory]
        [InlineData("wrong")]
        [InlineData("WrongCase")]
        public void ValidateEqualTo(string input)
        {
            // arrange
            var request = UnitTestFixture.CreateRequestMock();
            request.AddParameter(new Parameter("role", input, ParameterScope.Parameter));

            // act
            var validator = new RestApiValidator(request)
                .EqualTo("role", "admin");

            // validation
            Assert.False(validator.IsValid);
            Assert.Contains(validator.Result.Errors, e => e.Code == "MISMATCH");
        }

        /// <summary>
        /// Verifies that Range fails when integer value is outside valid range.
        /// </summary>
        [Theory]
        [InlineData("-1")]
        [InlineData("101")]
        public void ValidateRange(string input)
        {
            var request = UnitTestFixture.CreateRequestMock();
            request.AddParameter(new Parameter("level", input, ParameterScope.Parameter));

            var validator = new RestApiValidator(request)
                .Range("level", 0, 100);

            Assert.False(validator.IsValid);
            Assert.Contains(validator.Result.Errors, e => e.Code == "OUT_OF_RANGE");
        }

        /// <summary>
        /// Verifies that StartsWith fails when input does not begin with prefix.
        /// </summary>
        [Theory]
        [InlineData("abc123")]
        [InlineData("xyz-start")]
        public void ValidateStartsWith(string input)
        {
            var request = UnitTestFixture.CreateRequestMock();
            request.AddParameter(new Parameter("code", input, ParameterScope.Parameter));

            var validator = new RestApiValidator(request)
                .StartsWith("code", "start");

            Assert.False(validator.IsValid);
            Assert.Contains(validator.Result.Errors, e => e.Code == "PREFIX_MISMATCH");
        }

        /// <summary>
        /// Verifies that EndsWith fails when input does not end with suffix.
        /// </summary>
        [Theory]
        [InlineData("summary.txt")]
        [InlineData("document.pdf")]
        public void ValidateEndsWith(string input)
        {
            var request = UnitTestFixture.CreateRequestMock();
            request.AddParameter(new Parameter("filename", input, ParameterScope.Parameter));

            var validator = new RestApiValidator(request)
                .EndsWith("filename", ".log");

            Assert.False(validator.IsValid);
            Assert.Contains(validator.Result.Errors, e => e.Code == "SUFFIX_MISMATCH");
        }

        /// <summary>
        /// Verifies that In fails when value is not in allowed set.
        /// </summary>
        [Theory]
        [InlineData("guest")]
        [InlineData("anonymous")]
        public void ValidateIn(string input)
        {
            var request = UnitTestFixture.CreateRequestMock();
            request.AddParameter(new Parameter("role", input, ParameterScope.Parameter));

            var validator = new RestApiValidator(request)
                .In("role", "Admin", "Editor", "User");

            Assert.False(validator.IsValid);
            Assert.Contains(validator.Result.Errors, e => e.Code == "INVALID_CHOICE");
        }

        /// <summary>
        /// Verifies that Contains fails when input does not include required text.
        /// </summary>
        [Theory]
        [InlineData("this is unrelated")]
        [InlineData("foo bar")]
        public void ValidateContains(string input)
        {
            var request = UnitTestFixture.CreateRequestMock();
            request.AddParameter(new Parameter("description", input, ParameterScope.Parameter));

            var validator = new RestApiValidator(request)
                .Contains("description", "admin");

            Assert.False(validator.IsValid);
            Assert.Contains(validator.Result.Errors, e => e.Code == "MISSING_FRAGMENT");
        }

        public enum Difficulty { Easy, Medium, Hard }

        /// <summary>
        /// Verifies that MatchesEnum fails for invalid enum names.
        /// </summary>
        [Theory]
        [InlineData("Impossible")]
        [InlineData("easy-peasy")]
        public void ValidateMatchesEnum(string value)
        {
            var request = UnitTestFixture.CreateRequestMock();
            request.AddParameter(new Parameter("difficulty", value, ParameterScope.Parameter));

            var validator = new RestApiValidator(request)
                .MatchesEnum<Difficulty>("difficulty");

            Assert.False(validator.IsValid);
            Assert.Contains(validator.Result.Errors, e => e.Code == "INVALID_ENUM");
        }

        /// <summary>
        /// Verifies that IsDate fails when value cannot be parsed as date.
        /// </summary>
        [Theory]
        [InlineData("not-a-date")]
        [InlineData("31/31/2020")]
        public void ValidateIsDate(string input)
        {
            var request = UnitTestFixture.CreateRequestMock();
            request.AddParameter(new Parameter("date", input, ParameterScope.Parameter));

            var validator = new RestApiValidator(request)
                .IsDate("date");

            Assert.False(validator.IsValid);
            Assert.Contains(validator.Result.Errors, e => e.Code == "INVALID_DATE");
        }

        /// <summary>
        /// Verifies that Custom fails when the condition evaluates to false.
        /// </summary>
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("nonadmin")]
        public void ValidateCustom(string input)
        {
            var request = UnitTestFixture.CreateRequestMock();
            request.AddParameter(new Parameter("nickname", input, ParameterScope.Parameter));

            var validator = new RestApiValidator(request)
                .Custom(
                    r => r.GetParameter("nickname")?.Value == "admin",
                    "Only admins allowed",
                    "nickname",
                    "NOT_ADMIN"
                );

            Assert.False(validator.IsValid);
            Assert.Contains(validator.Result.Errors, e => e.Code == "NOT_ADMIN");
        }

        /// <summary>
        /// Verifies that Require is conditionally executed when When returns true.
        /// </summary>
        [Theory]
        [InlineData("true", null)]
        [InlineData("true", "")]
        public void ValidateWhen_ConditionalRequire(string subscribe, string email)
        {
            var request = UnitTestFixture.CreateRequestMock();
            request.AddParameter(new Parameter("subscribe", subscribe, ParameterScope.Parameter));
            request.AddParameter(new Parameter("email", email, ParameterScope.Parameter));

            var validator = new RestApiValidator(request)
                .When(r => r.GetParameter("subscribe")?.Value == "true")
                .Require("email", "Email is required when subscribing.");

            Assert.False(validator.IsValid);
            Assert.Contains(validator.Result.Errors, e => e.Field == "email");
        }

        /// <summary>
        /// Verifies that validation is skipped when When condition is false.
        /// </summary>
        [Theory]
        [InlineData("false", null)]
        [InlineData(null, "")]
        public void ValidateWhen_ConditionFalse(string subscribe, string email)
        {
            var request = UnitTestFixture.CreateRequestMock();
            request.AddParameter(new Parameter("subscribe", subscribe, ParameterScope.Parameter));
            request.AddParameter(new Parameter("email", email, ParameterScope.Parameter));

            var validator = new RestApiValidator(request)
                .When(r => r.GetParameter("subscribe")?.Value == "true")
                .Require("email", "Email required if subscribing.");

            Assert.True(validator.IsValid); // validation skipped
            Assert.Empty(validator.Result.Errors);
        }

    }
}
