using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using AspNetHttpLogger.Annotations;

namespace AspNetHttpLogger
{
    public class LogEvent
    {
        [PublicAPI] public readonly string AbsoluteUrl;
        [PublicAPI] public readonly IPAddress ClientIp;
        [PublicAPI] public readonly HttpStatusCode HttpStatusCode;
        [PublicAPI] public readonly int HttpStatusCodeNumber;
        [PublicAPI] public readonly string HttpStatusReasonPhrase;
        [PublicAPI] public readonly bool IsSuccess;
        [PublicAPI] public readonly string RelativeUrl;
        [PublicAPI, NotNull] public readonly HttpRequestMessage Request;
        [PublicAPI] public readonly string RequestContent;
        [PublicAPI] public readonly Guid RequestId;
        [PublicAPI, NotNull] public readonly HttpResponseMessage Response;
        [PublicAPI] public readonly string ResponseContent;
        [PublicAPI, NotNull] public readonly string ShortRequestId;
        [PublicAPI, NotNull] public readonly string Summary;
        [PublicAPI] public readonly string UserHostAddress;
        [PublicAPI, NotNull] public readonly string UserName;

        private readonly Lazy<string> _toStringLazy = new Lazy<string>(() => "");

        [UsedImplicitly]
        public LogEvent()
        {
        }

        public LogEvent([NotNull] HttpRequestMessage request, [NotNull] HttpResponseMessage response, Guid requestId,
            [NotNull] string shortRequestId,
            [NotNull] string userName, string userHostAddress, string requestContent, string responseContent,
            [NotNull] string summary)
        {
            if (request == null) throw new ArgumentNullException("request");
            if (response == null) throw new ArgumentNullException("response");
            if (shortRequestId == null) throw new ArgumentNullException("shortRequestId");
            if (userName == null) throw new ArgumentNullException("userName");
            if (summary == null) throw new ArgumentNullException("summary");

            RequestId = requestId;
            Request = request;
            Response = response;
            UserName = userName;
            UserHostAddress = userHostAddress;
            RelativeUrl = request.RequestUri.AbsolutePath;
            AbsoluteUrl = request.RequestUri.ToString();
            HttpStatusCodeNumber = (int) response.StatusCode;
            HttpStatusCode = response.StatusCode;
            HttpStatusReasonPhrase = response.ReasonPhrase;
            RequestContent = requestContent;
            ResponseContent = responseContent;
            Summary = summary;
            ShortRequestId = shortRequestId;

            IsSuccess = response.IsSuccessStatusCode;
            IPAddress.TryParse(UserHostAddress, out ClientIp);

            _toStringLazy = new Lazy<string>(GetToString);
        }

        private string GetToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine("REQUEST");
            sb.AppendLine("-------");
            sb.AppendFormat("{0} {1}\n", Request.Method.Method, AbsoluteUrl);
            AppendHeaders(sb, Request.Headers);
            sb.AppendLine("");
            sb.AppendLine(RequestContent);

            sb.AppendLine("");
            sb.AppendLine("");
            sb.AppendLine("RESPONSE");
            sb.AppendLine("--------");
            sb.AppendFormat("HTTP/{0} {1} {2}\n", Response.Version, (int) Response.StatusCode, Response.StatusCode);
            AppendHeaders(sb, Response.Headers);
            sb.AppendLine("");
            sb.AppendLine(ResponseContent);
            string str = sb.ToString();
            return str;
        }

        public override string ToString()
        {
            return _toStringLazy.Value;
        }

        // ReSharper disable once ParameterTypeCanBeEnumerable.Local
        private static void AppendHeaders(StringBuilder sb, HttpHeaders headers)
        {
            foreach (var headerNameToValues in headers)
            {
                string headerName = headerNameToValues.Key;
                sb.Append(headerName + ": ");

                IEnumerable<string> headerValues = headerNameToValues.Value;
                foreach (string value in headerValues)
                {
                    sb.Append(value);
                }

                sb.Append("\n");
            }
        }
    }
}