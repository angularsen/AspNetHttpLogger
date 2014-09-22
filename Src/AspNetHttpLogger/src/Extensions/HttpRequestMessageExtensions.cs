using System.Net;
using System.Net.Http;
using AspNetHttpLogger.Annotations;

namespace AspNetHttpLogger.Extensions
{
    /// <summary>
    ///     Extensions for HttpRequestMessage.
    /// </summary>
    /// <remarks>
    ///     Copied from http://stackoverflow.com/a/19849122/134761
    /// </remarks>
    [PublicAPI]
    public static class HttpRequestMessageExtensions
    {
        private const string HttpContext = "MS_HttpContext";

        private const string RemoteEndpointMessage =
            "System.ServiceModel.Channels.RemoteEndpointMessageProperty";

        private const string OwinContext = "MS_OwinContext";

        /// <summary>
        ///     Get client IP for a request in Web Api. Returns null if user host address
        ///     is not specified or cannot be parsed as IP.
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [PublicAPI]
        public static IPAddress GetClientIpAddress(this HttpRequestMessage request)
        {
            if (request == null)
                return null;

            string address = GetUserHostAddress(request);
            if (string.IsNullOrEmpty(address))
                return null;

            IPAddress ip;
            return IPAddress.TryParse(address, out ip) ? ip : null;
        }

        [PublicAPI]
        public static string GetUserHostAddress(this HttpRequestMessage request)
        {
            // Typical case when testing.
            if (request == null) return null;

            // Web-hosting. Needs reference to System.Web.dll
            if (request.Properties.ContainsKey(HttpContext))
            {
                dynamic ctx = request.Properties[HttpContext];
                if (ctx != null)
                {
                    return ctx.Request.UserHostAddress;
                }
            }

            // Self-hosting. Needs reference to System.ServiceModel.dll. 
            if (request.Properties.ContainsKey(RemoteEndpointMessage))
            {
                dynamic remoteEndpoint = request.Properties[RemoteEndpointMessage];
                if (remoteEndpoint != null)
                {
                    return remoteEndpoint.Address;
                }
            }

            // Self-hosting using Owin. Needs reference to Microsoft.Owin.dll. 
            if (request.Properties.ContainsKey(OwinContext))
            {
                dynamic owinContext = request.Properties[OwinContext];
                if (owinContext != null)
                {
                    return owinContext.Request.RemoteIpAddress;
                }
            }

            return null;
        }
    }
}