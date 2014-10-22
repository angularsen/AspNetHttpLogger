using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using AspNetHttpLogger.Annotations;
using AspNetHttpLogger.Extensions;

namespace AspNetHttpLogger
{
    [PublicAPI]
    public class LoggingHandler : DelegatingHandler
    {
        public delegate void ErrorEventHandler([NotNull] HttpResponseMessage response, [NotNull] Exception e);

        /// <summary>
        ///     Only log content for valid data content types, such as JSON and XML.
        /// </summary>
        private readonly string[] _contentTypeWhitelist;

        /// <summary>
        ///     Skip reading huge contents into memory, typically for file transfers. 5M chars.
        /// </summary>
        private readonly int _maxContentLength;

        /// <summary>
        ///     Truncate logged content to this length to avoid spamming the logging service. 100k chars.
        /// </summary>
        private readonly int _maxLoggedContentLength;

        [PublicAPI]
        public LoggingHandler(int maxContentLength = 5*1000*1000,
            int maxLoggedContentLength = 100*1000,
            string[] contentTypeWhitelist = null)
        {
            _contentTypeWhitelist = contentTypeWhitelist ??
                                    new[]
                                    {
                                        "application/json", "application/xml", "text/xml", "application/x-yaml",
                                        "text/yaml",
                                        "text/plain"
                                    };

            _maxLoggedContentLength = maxLoggedContentLength;
            _maxContentLength = maxContentLength;
        }

        public event Action<LogEvent> ResponseCompleted;
        public event ErrorEventHandler InternalError;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            HttpResponseMessage response = await base.SendAsync(request, cancellationToken);

            await LogResponse(response);

            return response;
        }

        private async Task LogResponse(HttpResponseMessage response)
        {
            if (response == null) throw new ArgumentNullException("response");

            HttpRequestMessage request = response.RequestMessage;

            try
            {
                string method = request.Method.Method;
                string relativeUrl = request.RequestUri.AbsolutePath;
                Guid requestId = GetCorrelationId(request);
                string shortRequestId = GetShortRequestId(requestId);
                string logSummary = string.Format("#{3} HTTP {0} - {1} {2}", (int) response.StatusCode, method, relativeUrl, shortRequestId);
                string userName = GetUserName(request) ?? "<no user>";

                string requestContent = await GetContentStringAsync(request.Content);
                string responseContent = await GetContentStringAsync(response.Content);

// ReSharper disable RedundantArgumentName
                var logEvent = new LogEvent(request: request, response: response,
                    requestId: requestId, shortRequestId: shortRequestId, userName: userName,
                    userHostAddress: request.GetUserHostAddress(),
                    requestContent: requestContent, responseContent: responseContent, summary: logSummary);
// ReSharper restore RedundantArgumentName

                RaiseResponseCompleted(logEvent);
            }
            catch (Exception e)
            {
                Trace.TraceError(e.ToString());
                RaiseError(response, e);
            }
        }

        private static string GetShortRequestId(Guid requestId)
        {
            // Request ID tends to start with many zeros, so truncating the first N characters are not very helpful.
            // Use hash hex as a short, readable representation for logging.
            // Important to take hash of string representation, not guid, since the hash would become equally unusable.
            string requestIdHash = requestId.ToString().GetHashCode().ToString("x8", CultureInfo.InvariantCulture);
            return requestIdHash;
        }


        private void RaiseError([NotNull] HttpResponseMessage response, [NotNull] Exception e)
        {
            if (response == null) throw new ArgumentNullException("response");
            if (e == null) throw new ArgumentNullException("e");
            try
            {
                ErrorEventHandler handler = InternalError;
                if (handler != null) handler(response, e);
            }
                // ReSharper disable once EmptyGeneralCatchClause
            catch
            {
                // Ignore
            }
        }

        private void RaiseResponseCompleted(LogEvent logEvent)
        {
            try
            {
                Action<LogEvent> handler = ResponseCompleted;
                if (handler != null) handler(logEvent);
            }
// ReSharper disable once EmptyGeneralCatchClause
            catch
            {
                // Ignore
            }
        }

        private async Task<string> GetContentStringAsync([CanBeNull] HttpContent content)
        {
            if (content == null || content.Headers.ContentLength.GetValueOrDefault() == 0)
            {
                return "<empty>";
            }

            MediaTypeHeaderValue contentTypeHeader = content.Headers.ContentType;
            if (contentTypeHeader == null)
            {
                return "<unknown content type>";
            }

            if (
                !_contentTypeWhitelist.Any(
                    t => string.Equals(t, contentTypeHeader.MediaType, StringComparison.OrdinalIgnoreCase)))
            {
                return "<content type skipped>";
            }

            long? contentLength = content.Headers.ContentLength;
            if (contentLength > _maxContentLength)
            {
                return "<too long content>";
            }

            string contentString = await content.ReadAsStringAsync();

            if (contentLength > _maxLoggedContentLength)
            {
                return contentString.Substring(0, _maxLoggedContentLength) + "\n---truncated---";
            }

            return contentString;
        }

        [CanBeNull]
        private static string GetUserName([NotNull] HttpRequestMessage request)
        {
            if (request == null) throw new ArgumentNullException("request");

            try
            {
                // Can be null when run on a background thread or worker role
                if (HttpContext.Current == null || HttpContext.Current.User == null ||
                    HttpContext.Current.User.Identity == null)
                {
                    return null;
                }

                string userName = HttpContext.Current.User.Identity.GetUserName();
                return userName;
            }
            catch (Exception e)
            {
                Trace.TraceError(e.ToString());
                return null;
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        /// <remarks>Copied from \packages\Microsoft.AspNet.WebApi.Core.5.2.2\lib\net45\System.Web.Http.dll</remarks>
        private static Guid GetCorrelationId([NotNull] HttpRequestMessage request)
        {
            if (request == null) throw new ArgumentNullException("request");

            Guid correlationId;
            const string requestCorrelationKey = "MS_RequestId";
            if (!request.Properties.TryGetValue(requestCorrelationKey, out correlationId))
            {
                // Check if the Correlation Manager ID is set; otherwise fallback to creating a new GUID
                correlationId = Trace.CorrelationManager.ActivityId;
                if (correlationId == Guid.Empty)
                {
                    correlationId = Guid.NewGuid();
                }

                request.Properties.Add(requestCorrelationKey, correlationId);
            }

            return correlationId;
        }
    }
}