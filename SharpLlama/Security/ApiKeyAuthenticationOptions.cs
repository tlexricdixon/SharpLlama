using Microsoft.AspNetCore.Authentication;

namespace SharpLlama.Security
{
    public class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
    {
        public string HeaderName { get; set; } = "X-API-Key";
    }
}