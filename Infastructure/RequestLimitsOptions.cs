namespace SharpLlama.Infrastructure;

public sealed class RequestLimitsOptions
{
    public long MaxRequestBodyBytes { get; set; } = 256 * 1024;
    public int MaxMessageChars { get; set; } = 4000;
    public int MaxMessages { get; set; } = 50;
}