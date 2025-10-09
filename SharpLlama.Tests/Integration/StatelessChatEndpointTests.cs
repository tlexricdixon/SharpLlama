using System.Net;
using System.Net.Http.Json;
using Entities;
using FluentAssertions;
using Xunit;

namespace SharpLlama.Tests.Integration;

public class StatelessChatEndpointTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact(Skip = "Requires model to run.")]
    public async Task SendEnhancedMessage_Returns_Ok()
    {
        // Add API key header example (modify existing tests accordingly)
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/StatelessChat/SendEnhanced")
        {
            Content = JsonContent.Create(new SendMessageInput { Text = "Hello" })
        };
        request.Headers.Add("X-API-Key", "DEMO-KEY-123");
        var resp = await _client.SendAsync(request);

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SendEnhancedMessage_Returns_Validation_Error_For_Empty_Text()
    {
        var input = new SendMessageInput { Text = string.Empty };
        var resp = await _client.PostAsJsonAsync("/api/StatelessChat/SendEnhanced", input);
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
