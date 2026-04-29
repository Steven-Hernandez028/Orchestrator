using FluentAssertions;
using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;

namespace Orchestrator.IntegrationTests.Api;

public class DevicesEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public DevicesEndpointTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetDevices_ShouldReturnOk()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/devices");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNull();
    }

    [Fact]
    public async Task RegisterDevice_WithValidData_ShouldReturn201()
    {
        var client = _factory.CreateClient();
        var request = new
        {
            deviceSerial = "TEST001",
            friendlyName = "Test Device",
            androidVersion = "9"
        };

        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/api/devices/register", content);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var responseContent = await response.Content.ReadAsStringAsync();
        responseContent.Should().Contain("TEST001");
    }

    [Fact]
    public async Task RegisterDevice_WithDuplicateSerial_ShouldReturnBadRequest()
    {
        var client = _factory.CreateClient();
        var request = new
        {
            deviceSerial = "DUPLICATE",
            friendlyName = "Device 1",
            androidVersion = "9"
        };

        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response1 = await client.PostAsync("/api/devices/register", content);
        response1.StatusCode.Should().Be(HttpStatusCode.Created);

        var response2 = await client.PostAsync("/api/devices/register", content);
        response2.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetDeviceById_WithValidId_ShouldReturnDevice()
    {
        var client = _factory.CreateClient();
        var request = new
        {
            deviceSerial = "TEST002",
            friendlyName = "Test Device 2",
            androidVersion = "9"
        };

        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var registerResponse = await client.PostAsync("/api/devices/register", content);
        registerResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var responseContent = await registerResponse.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(responseContent);
        var deviceId = doc.RootElement.GetProperty("id").GetString();

        var getResponse = await client.GetAsync($"/api/devices/{deviceId}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
