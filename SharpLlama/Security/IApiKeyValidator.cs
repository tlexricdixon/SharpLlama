namespace SharpLlama.Security;

public interface IApiKeyValidator
{
    bool TryValidate(string apiKey, out ApiKeyIdentity identity);
}