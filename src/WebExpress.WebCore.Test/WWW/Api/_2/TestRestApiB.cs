using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebRestApi;
using WebExpress.WebCore.WebStatusPage;

namespace WebExpress.WebCore.Test.WWW.Api._2
{
    /// <summary>
    /// A dummy class for testing purposes.
    /// </summary>
    public sealed class TestRestApiB : IRestApi
    {
        /// <summary>
        /// Gets or sets a synchronization hook for exercising real requests during host shutdown.
        /// </summary>
        internal static Action BeforeGet { get; set; }

        /// <summary>
        /// Initialization of the rest api resource. Here, for example, managed resources can be loaded. 
        /// </summary>
        /// <param name="restApiContext">The context of the restapi resource.</param>
        public TestRestApiB(IRestApiContext restApiContext)
        {
            // test the injection
            if (restApiContext is null)
            {
                throw new ArgumentNullException(nameof(restApiContext), "Parameter cannot be null or empty.");
            }
        }

        /// <summary>
        /// Creates data.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The response containing the result of the operation.</returns>
        public Response CreateData(Request request)
        {
            return new ResponseBadRequest(new StatusMessage("Not implemented."));
        }

        /// <summary>
        /// Gets data.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The response containing the result of the operation.</returns>
        [Method(RequestMethod.GET)]
        public Response GetData(Request request)
        {
            BeforeGet?.Invoke();
            return new ResponseBadRequest(new StatusMessage("Not implemented."));
        }

        /// <summary>
        /// Updates data.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The response containing the result of the operation.</returns>
        public Response UpdateData(Request request)
        {
            // test the request
            if (request is null)
            {
                throw new ArgumentNullException(nameof(request), "Parameter cannot be null or empty.");
            }

            return new ResponseBadRequest(new StatusMessage("Not implemented."));
        }

        /// <summary>
        /// Deletes data.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The response containing the result of the operation.</returns>
        public Response DeleteData(Request request)
        {
            // test the request
            if (request is null)
            {
                throw new ArgumentNullException(nameof(request), "Parameter cannot be null or empty.");
            }

            return new ResponseBadRequest(new StatusMessage("Not implemented."));
        }

        /// <summary>
        /// Release of unmanaged resources reserved during use.
        /// </summary>
        public void Dispose()
        {
        }
    }
}
