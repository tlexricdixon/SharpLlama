using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace SharpLlama.ChatUI.Services;

public sealed class ChatApiClient
{
    private readonly HttpClient _http;
    public ChatApiClient(HttpClient http) => _http = http;

    public void Configure(string baseUrl, string? apiKey)
    {
        _http.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
        _http.DefaultRequestHeaders.Clear();
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            // Match your backend header name (Program.cs shows "X-API-Key")
            _http.DefaultRequestHeaders.Add("X-API-Key", apiKey.Trim());
        }
    }

    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public async Task<string> SendAsync(string prompt, CancellationToken ct = default)
    {
        var payload = new { Text = prompt };
        using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        using var resp = await _http.PostAsync("api/StatefulChat/Send", content, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var error = await resp.Content.ReadAsStringAsync(ct);
            return $"[Error {(int)resp.StatusCode}] {error}";
        }
        var json = await resp.Content.ReadAsStringAsync(ct);
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("response", out var r))
                return r.GetString() ?? "";
            return json;
        }
        catch
        {
            return json;
        }
    }

    public async Task<bool> WarmupAsync(CancellationToken ct = default)
    {
        using var resp = await _http.PostAsync("api/warmup", content: null, ct);
        return resp.IsSuccessStatusCode;
    }
}

public sealed class ChatService
{
    private readonly ChatApiClient _api;
    private readonly ChatState _state;
    public ChatService(ChatApiClient api, ChatState state)
    {
        _api = api;
        _state = state;
    }

    public void Configure(string baseUrl, string? apiKey) => _api.Configure(baseUrl, apiKey);

    public async Task SendAsync(string prompt, CancellationToken ct = default)
    {
        _state.Add("user", prompt);
        var reply = await _api.SendAsync(prompt, ct);
        _state.Add("assistant", reply);
    }

    public async Task<bool> WarmupAsync(CancellationToken ct = default) => await _api.WarmupAsync(ct);
}