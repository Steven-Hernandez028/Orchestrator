using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Orchestrator.Core.Enums;
using Orchestrator.Core.Models;
using Orchestrator.Infrastructure.Data;
using Orchestrator.Infrastructure.Data.Repositories;
using Xunit;

namespace Orchestrator.UnitTests.Repositories;

public class DeviceRepositoryTests
{
    private OrchestratorDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<OrchestratorDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new OrchestratorDbContext(options);
    }

    [Fact]
    public async Task CreateAsync_ShouldCreateDevice()
    {
        using var context = CreateContext();
        var repo = new DeviceRepository(context);
        var device = new Device
        {
            DeviceSerial = "ABC123",
            FriendlyName = "Test Device",
            AndroidVersion = "9",
            State = DeviceState.Online
        };

        var result = await repo.CreateAsync(device);

        result.Id.Should().NotBeEmpty();
        result.DeviceSerial.Should().Be("ABC123");
        result.FriendlyName.Should().Be("Test Device");
        result.CreatedAt.Should().NotBe(default);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnDevice()
    {
        using var context = CreateContext();
        var repo = new DeviceRepository(context);
        var device = new Device { DeviceSerial = "ABC123", FriendlyName = "Test", AndroidVersion = "9", State = DeviceState.Online };
        await repo.CreateAsync(device);

        var result = await repo.GetByIdAsync(device.Id);

        result.Should().NotBeNull();
        result!.DeviceSerial.Should().Be("ABC123");
    }

    [Fact]
    public async Task GetBySerialAsync_ShouldReturnDevice()
    {
        using var context = CreateContext();
        var repo = new DeviceRepository(context);
        var device = new Device { DeviceSerial = "SERIAL123", FriendlyName = "Test", AndroidVersion = "9", State = DeviceState.Online };
        await repo.CreateAsync(device);

        var result = await repo.GetBySerialAsync("SERIAL123");

        result.Should().NotBeNull();
        result!.DeviceSerial.Should().Be("SERIAL123");
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnAllDevices()
    {
        using var context = CreateContext();
        var repo = new DeviceRepository(context);
        var device1 = new Device { DeviceSerial = "ABC1", FriendlyName = "Test1", AndroidVersion = "9", State = DeviceState.Online };
        var device2 = new Device { DeviceSerial = "ABC2", FriendlyName = "Test2", AndroidVersion = "9", State = DeviceState.Online };
        await repo.CreateAsync(device1);
        await repo.CreateAsync(device2);

        var result = await repo.GetAllAsync();

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task UpdateAsync_ShouldUpdateDevice()
    {
        using var context = CreateContext();
        var repo = new DeviceRepository(context);
        var device = new Device { DeviceSerial = "ABC123", FriendlyName = "Test", AndroidVersion = "9", State = DeviceState.Online };
        await repo.CreateAsync(device);

        device.FriendlyName = "Updated";
        device.State = DeviceState.Executing;
        var result = await repo.UpdateAsync(device);

        result.FriendlyName.Should().Be("Updated");
        result.State.Should().Be(DeviceState.Executing);
    }

    [Fact]
    public async Task DeleteAsync_ShouldRemoveDevice()
    {
        using var context = CreateContext();
        var repo = new DeviceRepository(context);
        var device = new Device { DeviceSerial = "ABC123", FriendlyName = "Test", AndroidVersion = "9", State = DeviceState.Online };
        await repo.CreateAsync(device);

        var result = await repo.DeleteAsync(device.Id);

        result.Should().BeTrue();
        var fetched = await repo.GetByIdAsync(device.Id);
        fetched.Should().BeNull();
    }
}
