using System.Collections.Concurrent;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace SharpLlama.Security;


public sealed record ApiIdentity(string UserName, string ApiKey);

/// <summary>
/// Simple in-memory API key validator.
/// Expects configuration section:
/// {
///   "ApiKeys": [
///     { "key": "abc123", "userName": "internal-service" },
///     { "key": "def456", "userName": "test-client" }
///   ]
/// }
/// </summary>
public sealed class ApiKeyValidator : IApiKeyValidator
{
    private readonly ConcurrentDictionary<string, ApiIdentity> _keys = new(StringComparer.Ordinal);

    public ApiKeyValidator(IConfiguration configuration, ILogger<ApiKeyValidator> logger)
    {
        var section = configuration.GetSection("ApiKeys");
        if (!section.Exists())
        {
            logger.LogWarning("ApiKeys configuration section missing. No API keys registered.");
            return;
        }

        foreach (var child in section.GetChildren())
        {
            var key = child.GetValue<string>("key") ?? child.GetValue<string>("Key");
            var user = child.GetValue<string>("userName") ?? child.GetValue<string>("UserName");
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(user))
            {
                logger.LogWarning("Skipping invalid ApiKeys entry (missing key or userName).");
                continue;
            }

            if (_keys.TryAdd(key, new ApiIdentity(user, key)))
            {
                logger.LogDebug("Registered API key for user {UserName}.", user);
            }
        }

        if (_keys.IsEmpty)
            logger.LogWarning("No valid API keys loaded.");
    }

    public bool TryValidate(string apiKey, out ApiKeyIdentity identity)
    {
        identity = default!;
        if (string.IsNullOrWhiteSpace(apiKey))
            return false;

        // O(1) lookup; if concerned about timing attacks, introduce constant-time comparison.
        if (_keys.TryGetValue(apiKey, out var apiIdentity))
        {
            identity = new ApiKeyIdentity(
                apiIdentity.ApiKey,
                apiIdentity.UserName,
                Array.Empty<string>() // or assign roles if available
            );
            return true;
        }
        return false;
    }
}