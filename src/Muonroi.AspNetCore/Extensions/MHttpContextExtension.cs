namespace Muonroi.AspNetCore.Extensions;

public static class MHttpContextExtension
{
    public static string GetRequestedIpAddress(this HttpContext httpContext)
    {
        string? result = string.Empty;
        try
        {
            //first try to get IP address from the forwarded header
            //the X-Forwarded-For (XFF) HTTP header field is a de facto standard for identifying the originating IP address of a client
            //connecting to a web server through an HTTP proxy or load balancer
            const string forwardedHttpHeaderKey = "X-FORWARDED-FOR";

            StringValues forwardedHeader = httpContext.Request.Headers[forwardedHttpHeaderKey];
            if (!StringValues.IsNullOrEmpty(forwardedHeader))
            {
                result = forwardedHeader.FirstOrDefault();
            }

            //if this header not exists try get connection remote IP address
            if (string.IsNullOrEmpty(result) && httpContext.Connection.RemoteIpAddress is not null)
            {
                result = httpContext.Connection.RemoteIpAddress.ToString();
            }
        }
        catch
        {
            return string.Empty;
        }

        //some of the validation
        if (result is not null &&
            result.Equals(IPAddress.IPv6Loopback.ToString(), StringComparison.InvariantCultureIgnoreCase))
        {
            result = IPAddress.Loopback.ToString();
        }

        //"TryParse" doesn't support IPv4 with port number
        if (IPAddress.TryParse(result ?? string.Empty, out IPAddress? ip))
        {
            //IP address is valid
            result = ip.ToString();
        }
        else if (!string.IsNullOrEmpty(result))
        {
            //remove port
            result = result.Split(':').FirstOrDefault();
        }

        return result ?? string.Empty;
    }

    public static string GetHeaderUserAgent(this HttpContext httpContext)
    {
        return httpContext.Request.Headers[HeaderNames.UserAgent].ToString();
    }
}
