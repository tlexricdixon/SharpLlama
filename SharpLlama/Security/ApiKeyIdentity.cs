namespace SharpLlama.Security;

public sealed record ApiKeyIdentity(string ApiKey, string UserName, string[] Roles);