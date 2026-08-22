using Microsoft.AspNetCore.Http;

namespace Infrastructure.Context
{
    public interface IApiContext
    {
        string CurrentUserId { get; }

        /// <summary>
        /// The caller's id, or null when the gateway attributed no user to the request. Unlike
        /// <see cref="CurrentUserId"/> this does not throw, so an endpoint can answer 401 rather
        /// than 500 for an unattributed call.
        /// </summary>
        string? CurrentUserIdOrNull { get; }

        /// <summary>
        /// True when the gateway attributed this request to an internal staff account. The gateway
        /// strips the header from anything inbound before stamping it.
        /// </summary>
        bool IsStaff { get; }

        string Culture { get; }
        string? Channel { get; }

        string? IpAddress { get; }
    }

    public class ApiContext : IApiContext
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public ApiContext(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public string CurrentUserId => ReadFromHeader("x-user-id") ?? throw new Exception("User id not found");
        public string? CurrentUserIdOrNull => ReadFromHeader("x-user-id");

        public bool IsStaff =>
            string.Equals(ReadFromHeader("x-user-type"), "Staff", StringComparison.OrdinalIgnoreCase);

        public string Culture => ReadFromHeader("x-culture") ?? "en-US";
        public string? Channel => ReadFromHeader("x-channel");

        public string? IpAddress
        {
            get
            {
                var ip = ReadFromHeader("cf-connecting-ip");
                if (!string.IsNullOrEmpty(ip))
                    return ip;

                //get Ip from request
                if (_httpContextAccessor.HttpContext?.Connection.RemoteIpAddress != null)
                    return _httpContextAccessor.HttpContext.Connection.RemoteIpAddress.ToString();

                return null;
            }
        }

        private string? ReadFromHeader(string headerName)
        {
            if (_httpContextAccessor.HttpContext == null)
                return null;

            if (_httpContextAccessor.HttpContext.Request.Headers.TryGetValue(headerName, out var value))
                return value.ToString();
            return null;
        }
    }
}