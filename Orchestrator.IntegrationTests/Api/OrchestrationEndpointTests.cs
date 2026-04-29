using FluentAssertions;
using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;

namespace Orchestrator.IntegrationTests.Api;

public class OrchestrationEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public OrchestrationEndpointTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task AssignScript_WithValidIds_ShouldReturnAccepted()
    {
        var client = _factory.CreateClient();

        // Register device
        var deviceRequest = new
        {
            deviceSerial = "ASSIGN_TEST",
            friendlyName = "Test",
            androidVersion = "9"
        };
        var deviceJson = JsonSerializer.Serialize(deviceRequest);
        var deviceContent = new StringContent(deviceJson, Encoding.UTF8, "application/json");
        var deviceResponse = await client.PostAsync("/api/devices/register", deviceContent);
        var deviceResponseContent = await deviceResponse.Content.ReadAsStringAsync();
        var deviceDoc = JsonDocument.Parse(deviceResponseContent);
        var deviceId = deviceDoc.RootElement.GetProperty("id").GetString();

        // Create script
        var scriptRequest = new
        {
            name = "Assignment Test",
            jsonDefinition = "{}"
        };
        var scriptJson = JsonSerializer.Serialize(scriptRequest);
        var scriptContent = new StringContent(scriptJson, Encoding.UTF8, "application/json");
        var scriptResponse = await client.PostAsync("/api/scripts", scriptContent);
        var scriptResponseContent = await scriptResponse.Content.ReadAsStringAsync();
        var scriptDoc = JsonDocument.Parse(scriptResponseContent);
        var scriptId = scriptDoc.RootElement.GetProperty("id").GetString();

        // Assign
        var assignRequest = new
        {
            deviceId = deviceId,
            scriptId = scriptId
        };
        var assignJson = JsonSerializer.Serialize(assignRequest);
        var assignContent = new StringContent(assignJson, Encoding.UTF8, "application/json");
        var assignResponse = await client.PostAsync("/api/orchestration/assign", assignContent);

        assignResponse.StatusCode.Should().Be(HttpStatusCode.Accepted);
    }

    [Fact]
    public async Task BroadcastScript_ShouldReturnAccepted()
    {
        var client = _factory.CreateClient();

        // Create script
        var scriptRequest = new
        {
            name = "Broadcast Test",
            jsonDefinition = "{}"
        };
        var scriptJson = JsonSerializer.Serialize(scriptRequest);
        var scriptContent = new StringContent(scriptJson, Encoding.UTF8, "application/json");
        var scriptResponse = await client.PostAsync("/api/scripts", scriptContent);
        var scriptResponseContent = await scriptResponse.Content.ReadAsStringAsync();
        var scriptDoc = JsonDocument.Parse(scriptResponseContent);
        var scriptId = scriptDoc.RootElement.GetProperty("id").GetString();

        // Broadcast
        var broadcastRequest = new
        {
            scriptId = scriptId
        };
        var broadcastJson = JsonSerializer.Serialize(broadcastRequest);
        var broadcastContent = new StringContent(broadcastJson, Encoding.UTF8, "application/json");
        var broadcastResponse = await client.PostAsync("/api/orchestration/broadcast", broadcastContent);

        broadcastResponse.StatusCode.Should().Be(HttpStatusCode.Accepted);
    }

    [Fact]
    public async Task PauseExecution_ShouldReturnAccepted()
    {
        var client = _factory.CreateClient();

        // Register device
        var deviceRequest = new
        {
            deviceSerial = "PAUSE_TEST",
            friendlyName = "Test",
            androidVersion = "9"
        };
        var deviceJson = JsonSerializer.Serialize(deviceRequest);
        var deviceContent = new StringContent(deviceJson, Encoding.UTF8, "application/json");
        var deviceResponse = await client.PostAsync("/api/devices/register", deviceContent);
        var deviceResponseContent = await deviceResponse.Content.ReadAsStringAsync();
        var deviceDoc = JsonDocument.Parse(deviceResponseContent);
        var deviceId = deviceDoc.RootElement.GetProperty("id").GetString();

        // Pause
        var pauseRequest = new
        {
            deviceId = deviceId
        };
        var pauseJson = JsonSerializer.Serialize(pauseRequest);
        var pauseContent = new StringContent(pauseJson, Encoding.UTF8, "application/json");
        var pauseResponse = await client.PostAsync("/api/orchestration/pause", pauseContent);

        pauseResponse.StatusCode.Should().Be(HttpStatusCode.Accepted);
    }
}
