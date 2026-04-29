# Testing Strategy & Test Structure

## Overview

**Unit Tests:** Repository + service logic (isolated, fast, Moq for dependencies)  
**Integration Tests:** REST API endpoints (full pipeline, in-memory DB)  
**Manual:** Offline resilience, 20-device broadcast, power cycles

---

## Unit Tests

Location: `Orchestrator.UnitTests/`

### Repository Tests

#### DeviceRepositoryTests.cs
```csharp
public class DeviceRepositoryTests
{
    private OrchestratorDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<OrchestratorDbContext>()
            .UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}")
            .Options;
        return new OrchestratorDbContext(options);
    }

    [Fact]
    public async Task CreateAsync_ShouldCreateDevice()
    {
        // Arrange
        using var db = CreateInMemoryDb();
        var repo = new DeviceRepository(db);
        var device = new Device { Id = "1", DeviceSerial = "TEST123", ... };

        // Act
        var result = await repo.CreateAsync(device);

        // Assert
        result.Id.Should().Be("1");
        db.Devices.Count().Should().Be(1);
    }

    [Fact]
    public async Task GetBySerialAsync_ShouldReturnDevice()
    {
        // Arrange
        using var db = CreateInMemoryDb();
        db.Devices.Add(new Device { Id = "1", DeviceSerial = "TEST123", ... });
        await db.SaveChangesAsync();
        var repo = new DeviceRepository(db);

        // Act
        var result = await repo.GetBySerialAsync("TEST123");

        // Assert
        result.Should().NotBeNull();
        result.DeviceSerial.Should().Be("TEST123");
    }
}
```

**Tested methods:**
- `CreateAsync()` → device persisted
- `GetByIdAsync()` → returns device by ID
- `GetBySerialAsync()` → returns device by serial (critical for registration)
- `GetAllAsync()` → lists all devices
- `UpdateAsync()` → updates device fields
- `DeleteAsync()` → removes device

---

#### ScriptRepositoryTests.cs
```csharp
[Fact]
public async Task CreateAsync_ShouldCreateScriptWithVersion1()
{
    // Arrange
    using var db = CreateInMemoryDb();
    var repo = new ScriptRepository(db);
    var script = new Script { Id = "1", Name = "Test", JsonDefinition = "{}" };

    // Act
    var result = await repo.CreateAsync(script);

    // Assert
    result.Version.Should().Be(1);
}

[Fact]
public async Task UpdateAsync_ShouldIncrementVersion()
{
    // Arrange
    using var db = CreateInMemoryDb();
    db.Scripts.Add(new Script { Id = "1", Version = 1, Name = "Original", ... });
    await db.SaveChangesAsync();
    var repo = new ScriptRepository(db);
    var script = db.Scripts.First();
    script.Name = "Updated";

    // Act
    var result = await repo.UpdateAsync(script);

    // Assert
    result.Version.Should().Be(2);
}
```

**Key test:** Version increment on update (proof of immutability).

---

#### LogRepositoryTests.cs
```csharp
[Fact]
public async Task CreateAsync_ShouldCreateLogWithSyncedFalse()
{
    // Arrange
    using var db = CreateInMemoryDb();
    var repo = new LogRepository(db);
    var log = new DeviceLog { ... };

    // Act
    var result = await repo.CreateAsync(log);

    // Assert
    result.Synced.Should().BeFalse(); // Critical for offline buffering
}

[Fact]
public async Task GetUnsyncdAsync_ShouldReturnBufferedLogs()
{
    // Arrange
    using var db = CreateInMemoryDb();
    db.DeviceLogs.Add(new DeviceLog { Synced = false, ... });
    db.DeviceLogs.Add(new DeviceLog { Synced = true, ... });
    await db.SaveChangesAsync();
    var repo = new LogRepository(db);

    // Act
    var result = await repo.GetUnsyncdAsync();

    // Assert
    result.Should().HaveCount(1);
    result.First().Synced.Should().BeFalse();
}

[Fact]
public async Task MarkSyncedAsync_ShouldUpdateFlag()
{
    // Arrange
    var logIds = new[] { "log1", "log2" };
    // ... setup ...
    var repo = new LogRepository(db);

    // Act
    await repo.MarkSyncedAsync(logIds);

    // Assert
    var marked = db.DeviceLogs.Where(l => logIds.Contains(l.Id)).ToList();
    marked.Should().AllSatisfy(l => l.Synced.Should().BeTrue());
}
```

**Critical test:** Offline buffering logic (Synced=false → true)

---

### Service Tests

#### OrchestrationServiceTests.cs
```csharp
public class OrchestrationServiceTests
{
    [Fact]
    public async Task AssignScriptAsync_WithValidIds_ShouldPublishCommand()
    {
        // Arrange
        var mockDeviceRepo = new Mock<IDeviceRepository>();
        var mockScriptRepo = new Mock<IScriptRepository>();
        var mockMqttPublisher = new Mock<IMqttPublisher>();

        mockDeviceRepo.Setup(r => r.GetByIdAsync("device1"))
            .ReturnsAsync(new Device { Id = "device1" });
        mockScriptRepo.Setup(r => r.GetByIdAsync("script1"))
            .ReturnsAsync(new Script { Id = "script1", Version = 1 });

        var service = new OrchestrationService(
            mockDeviceRepo.Object,
            mockScriptRepo.Object,
            mockMqttPublisher.Object
        );

        // Act
        await service.AssignScriptAsync("device1", "script1");

        // Assert
        mockMqttPublisher.Verify(
            p => p.PublishAsync(
                It.Is<string>(t => t.Contains("device1/commands")),
                It.IsAny<string>(),
                It.IsAny<int>()
            ),
            Times.Once
        );
    }

    [Fact]
    public async Task AssignScriptAsync_WithInvalidDevice_ShouldNotPublish()
    {
        // Arrange
        var mockDeviceRepo = new Mock<IDeviceRepository>();
        mockDeviceRepo.Setup(r => r.GetByIdAsync(It.IsAny<string>()))
            .ReturnsAsync((Device)null);

        var service = new OrchestrationService(mockDeviceRepo.Object, ...);

        // Act
        Func<Task> act = () => service.AssignScriptAsync("invalid", "script1");

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task BroadcastScriptAsync_ShouldPublishToAllDevices()
    {
        // Arrange
        var mockMqttPublisher = new Mock<IMqttPublisher>();
        var service = new OrchestrationService(..., mockMqttPublisher.Object);

        // Act
        await service.BroadcastScriptAsync("script1");

        // Assert
        mockMqttPublisher.Verify(
            p => p.PublishAsync(
                It.Is<string>(t => t.Contains("broadcast/commands")),
                ...
            ),
            Times.Once
        );
    }

    [Fact]
    public async Task HandleDeviceStatusAsync_ShouldUpdateLastSeen()
    {
        // Arrange
        var mockDeviceRepo = new Mock<IDeviceRepository>();
        var device = new Device { Id = "device1", LastSeen = DateTime.MinValue };
        mockDeviceRepo.Setup(r => r.GetByIdAsync("device1")).ReturnsAsync(device);
        mockDeviceRepo.Setup(r => r.UpdateAsync(It.IsAny<Device>())).ReturnsAsync(device);

        var service = new OrchestrationService(mockDeviceRepo.Object, ...);

        // Act
        await service.HandleDeviceStatusAsync(new { deviceId = "device1" });

        // Assert
        mockDeviceRepo.Verify(r => r.UpdateAsync(It.IsAny<Device>()), Times.Once);
    }
}
```

**Key assertions:**
- MQTT publish called with correct topic/payload
- Invalid device input throws exception
- Broadcast publishes to `broadcast/commands`
- Status updates LastSeen timestamp

---

## Integration Tests

Location: `Orchestrator.IntegrationTests/`

### CustomWebApplicationFactory

Factory pattern for test isolation. Each test gets fresh in-memory DB.

```csharp
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove SQLite DbContext
            var dbContextDescriptors = services.Where(x =>
                x.ServiceType == typeof(DbContextOptions<OrchestratorDbContext>)
                || (x.ServiceType.IsGenericType && 
                    x.ServiceType.GetGenericTypeDefinition() == typeof(DbContextOptions<>) &&
                    x.ServiceType.GetGenericArguments()[0] == typeof(OrchestratorDbContext)))
                .ToList();

            foreach (var descriptor in dbContextDescriptors)
                services.Remove(descriptor);

            // Add in-memory DB (unique per test)
            services.AddDbContext<OrchestratorDbContext>(options =>
                options.UseInMemoryDatabase($"InMemoryTestDb_{Guid.NewGuid():N}"));
        });

        builder.UseEnvironment("Testing");
    }
}
```

**Why:** Each test gets isolated database → no cross-test pollution

### API Endpoint Tests

#### DevicesEndpointTests.cs
```csharp
public class DevicesEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public DevicesEndpointTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task RegisterDevice_WithValidData_ShouldReturn201()
    {
        // Arrange
        var client = _factory.CreateClient();
        var request = new
        {
            deviceSerial = "R38M717AB0C",
            friendlyName = "Lab Phone 1",
            androidVersion = "9"
        };
        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await client.PostAsync("/api/devices/register", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var responseContent = await response.Content.ReadAsStringAsync();
        responseContent.Should().Contain("R38M717AB0C");
    }

    [Fact]
    public async Task RegisterDevice_WithDuplicateSerial_ShouldReturnBadRequest()
    {
        // Arrange
        var client = _factory.CreateClient();
        var request = new
        {
            deviceSerial = "R38M717AB0C",
            friendlyName = "Lab Phone 1",
            androidVersion = "9"
        };
        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act: register first time
        await client.PostAsync("/api/devices/register", content);

        // Act: register again with same serial
        var response = await client.PostAsync("/api/devices/register", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetDevices_ShouldReturnOk()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/devices");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
```

**Pattern:** Setup data → HTTP call → assert status + response content

---

#### ScriptsEndpointTests.cs
```csharp
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
    var createRequest = new { name = "Original", jsonDefinition = "{}" };
    var json = JsonSerializer.Serialize(createRequest);
    var content = new StringContent(json, Encoding.UTF8, "application/json");
    var createResponse = await client.PostAsync("/api/scripts", content);
    var doc = JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync());
    var scriptId = doc.RootElement.GetProperty("id").GetString();

    // Update
    var updateRequest = new { name = "Updated", jsonDefinition = "{}" };
    var updateJson = JsonSerializer.Serialize(updateRequest);
    var updateContent = new StringContent(updateJson, Encoding.UTF8, "application/json");
    var updateResponse = await client.PutAsync($"/api/scripts/{scriptId}", updateContent);

    updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    var updateDoc = JsonDocument.Parse(await updateResponse.Content.ReadAsStringAsync());
    var version = updateDoc.RootElement.GetProperty("version").GetInt32();
    version.Should().Be(2);
}
```

**Key test:** Version increment across API calls

---

#### OrchestrationEndpointTests.cs
```csharp
[Fact]
public async Task AssignScript_WithValidIds_ShouldReturnAccepted()
{
    var client = _factory.CreateClient();

    // Register device
    var deviceRequest = new { deviceSerial = "ASSIGN_TEST", friendlyName = "Test", androidVersion = "9" };
    var deviceJson = JsonSerializer.Serialize(deviceRequest);
    var deviceContent = new StringContent(deviceJson, Encoding.UTF8, "application/json");
    var deviceResponse = await client.PostAsync("/api/devices/register", deviceContent);
    var deviceDoc = JsonDocument.Parse(await deviceResponse.Content.ReadAsStringAsync());
    var deviceId = deviceDoc.RootElement.GetProperty("id").GetString();

    // Create script
    var scriptRequest = new { name = "Test", jsonDefinition = "{}" };
    var scriptJson = JsonSerializer.Serialize(scriptRequest);
    var scriptContent = new StringContent(scriptJson, Encoding.UTF8, "application/json");
    var scriptResponse = await client.PostAsync("/api/scripts", scriptContent);
    var scriptDoc = JsonDocument.Parse(await scriptResponse.Content.ReadAsStringAsync());
    var scriptId = scriptDoc.RootElement.GetProperty("id").GetString();

    // Assign
    var assignRequest = new { deviceId = deviceId, scriptId = scriptId };
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
    var scriptRequest = new { name = "Broadcast Test", jsonDefinition = "{}" };
    var scriptJson = JsonSerializer.Serialize(scriptRequest);
    var scriptContent = new StringContent(scriptJson, Encoding.UTF8, "application/json");
    var scriptResponse = await client.PostAsync("/api/scripts", scriptContent);
    var scriptDoc = JsonDocument.Parse(await scriptResponse.Content.ReadAsStringAsync());
    var scriptId = scriptDoc.RootElement.GetProperty("id").GetString();

    // Broadcast
    var broadcastRequest = new { scriptId = scriptId };
    var broadcastJson = JsonSerializer.Serialize(broadcastRequest);
    var broadcastContent = new StringContent(broadcastJson, Encoding.UTF8, "application/json");
    var broadcastResponse = await client.PostAsync("/api/orchestration/broadcast", broadcastContent);

    broadcastResponse.StatusCode.Should().Be(HttpStatusCode.Accepted);
}
```

**Critical test:** Full flow: register → create script → assign/broadcast → status check

---

## Test Execution

```bash
# Run all tests
dotnet test Orchestrator.sln

# Run unit tests only
dotnet test Orchestrator.UnitTests

# Run integration tests only
dotnet test Orchestrator.IntegrationTests

# Run specific test class
dotnet test Orchestrator.IntegrationTests --filter "ClassName=DevicesEndpointTests"

# Verbose output
dotnet test --verbosity detailed
```

**Expected output:**
```
Unit Tests:    19 passed ✓
Integration Tests: 12 passed ✓
Total: 31 passed, 0 failed
```

---

## Manual Testing (Not Automated)

### Offline Resilience Test
1. Install APK on device → register with backend
2. Push script to device
3. Kill backend process (or unplug Ethernet)
4. Verify device continues executing script (Logcat)
5. Restart backend → verify logs synced to DB

### Broadcast Test
1. Register 20 devices (or simulate with loopback MQTT)
2. Broadcast script via `/api/orchestration/broadcast`
3. Verify all 20 devices ACK within 5 seconds

### Power Cycle Test
1. Start script on device
2. Force reboot (adb reboot)
3. Verify BootReceiver relaunches service
4. Verify script resumes

### Metrics Collection Test
1. Script running
2. Query `/api/metrics/{deviceId}/latest` every 30s
3. Verify CPU/RAM/battery updates

---

## Coverage Goals

| Layer | Target |
|-------|--------|
| Repositories | 90%+ (unit) |
| Services | 80%+ (unit + integration) |
| Controllers | 70%+ (integration) |
| Models | 95%+ (unit) |

Run coverage report:
```bash
dotnet test /p:CollectCoverage=true /p:CoverageFormat=opencover
```

---

## Debugging Failed Tests

**InMemory DB cross-contamination:**
- Root cause: Reused database name across tests
- Fix: Use `Guid.NewGuid()` in CustomWebApplicationFactory ✓

**Serialization errors in integration tests:**
- Root cause: JsonDocument parsing failures
- Fix: Log response content before parsing, validate JSON structure

**MQTT publish not firing:**
- Root cause: Mock not set up correctly
- Fix: Verify mock setup with `.Verify(...)` assertion

**Entity not found after save:**
- Root cause: SaveChangesAsync not awaited
- Fix: Ensure all EF Core operations are async/awaited
