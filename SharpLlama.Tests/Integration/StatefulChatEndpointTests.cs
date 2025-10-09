using System.Net;
using System.Net.Http.Json;
using Entities;
using FluentAssertions;
using Xunit;

namespace SharpLlama.Tests.Integration;

public class StatefulChatEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public StatefulChatEndpointTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact(Skip="Requires actual model weights to run successfully.")]
    public async Task SendMessage_Returns_Ok_For_Valid_Request()
    {
        var input = new SendMessageInput { Text = "Hello" };
        var resp = await _client.PostAsJsonAsync("/api/StatefulChat/Send", input);
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SendMessage_Returns_Validation_Error_For_Empty_Text()
    {
        var input = new SendMessageInput { Text = string.Empty };
        var resp = await _client.PostAsJsonAsync("/api/StatefulChat/Send", input);
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
