using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

namespace SharpLlama.Security
{
    public class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationOptions>
    {
        private readonly IApiKeyValidator _apiKeyValidator;

        public ApiKeyAuthenticationHandler(
            IOptionsMonitor<ApiKeyAuthenticationOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder,
            ISystemClock clock,
            IApiKeyValidator apiKeyValidator)
            : base(options, logger, encoder, clock)
        {
            _apiKeyValidator = apiKeyValidator;
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue(Options.HeaderName, out var apiKeyHeader))
            {
                return Task.FromResult(AuthenticateResult.Fail("API Key was not provided."));
            }

            var apiKey = apiKeyHeader.ToString().Trim();
            if (!_apiKeyValidator.TryValidate(apiKey, out var identity))
            {
                return Task.FromResult(AuthenticateResult.Fail("Invalid API Key."));
            }

            var claims = new[] { new Claim(ClaimTypes.Name, identity.UserName) };
            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name));
            var ticket = new AuthenticationTicket(principal, Scheme.Name);

            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}