using FluentAssertions;
using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;

namespace Orchestrator.IntegrationTests.Api;

public class ScriptsEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public ScriptsEndpointTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetScripts_ShouldReturnOk()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/scripts");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CreateScript_WithValidData_ShouldReturn201()
    {
        var client = _factory.CreateClient();
        var request = new
        {
            name = "Test Script",
            jsonDefinition = "{\"steps\": []}"
        };

        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/api/scripts", content);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var responseContent = await response.Content.ReadAsStringAsync();
        responseContent.Should().Contain("Test Script");
    }

    [Fact]
    public async Task UpdateScript_ShouldIncrementVersion()
    {
        var client = _factory.CreateClient();

        // Create
        var createRequest = new
        {
            name = "Original Script",
            jsonDefinition = "{\"steps\": []}"
        };
        var json = JsonSerializer.Serialize(createRequest);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var createResponse = await client.PostAsync("/api/scripts", content);
        var responseContent = await createResponse.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(responseContent);
        var scriptId = doc.RootElement.GetProperty("id").GetString();

        // Update
        var updateRequest = new
        {
            name = "Updated Script",
            jsonDefinition = "{\"steps\": [{\"type\": \"WAIT\"}]}"
        };
        var updateJson = JsonSerializer.Serialize(updateRequest);
        var updateContent = new StringContent(updateJson, Encoding.UTF8, "application/json");
        var updateResponse = await client.PutAsync($"/api/scripts/{scriptId}", updateContent);

        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var updateResponseContent = await updateResponse.Content.ReadAsStringAsync();
        var updateDoc = JsonDocument.Parse(updateResponseContent);
        var version = updateDoc.RootElement.GetProperty("version").GetInt32();
        version.Should().Be(2);
    }

    [Fact]
    public async Task DeleteScript_ShouldRemoveScript()
    {
        var client = _factory.CreateClient();

        // Create
        var request = new
        {
            name = "Script To Delete",
            jsonDefinition = "{}"
        };
        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var createResponse = await client.PostAsync("/api/scripts", content);
        var responseContent = await createResponse.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(responseContent);
        var scriptId = doc.RootElement.GetProperty("id").GetString();

        // Delete
        var deleteResponse = await client.DeleteAsync($"/api/scripts/{scriptId}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify deleted
        var getResponse = await client.GetAsync($"/api/scripts/{scriptId}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
