using System.ComponentModel.DataAnnotations;

namespace Infrastructure;

public sealed class OtlpOptions
{
    [Url] public string MetricsEndpoint { get; set; } = "http://localhost:4318";
    [Url] public string TracesEndpoint  { get; set; } = "http://localhost:4317";
}