using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Orchestrator.Core.Models;
using Orchestrator.Infrastructure.Data;
using Orchestrator.Infrastructure.Data.Repositories;
using Xunit;

namespace Orchestrator.UnitTests.Repositories;

public class LogRepositoryTests
{
    private OrchestratorDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<OrchestratorDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new OrchestratorDbContext(options);
    }

    [Fact]
    public async Task CreateAsync_ShouldCreateLog()
    {
        using var context = CreateContext();
        var repo = new LogRepository(context);
        var deviceId = Guid.NewGuid();
        var log = new DeviceLog
        {
            DeviceId = deviceId,
            Level = "INFO",
            Message = "Test log"
        };

        var result = await repo.CreateAsync(log);

        result.Id.Should().NotBeEmpty();
        result.Message.Should().Be("Test log");
        result.Synced.Should().BeFalse();
    }

    [Fact]
    public async Task GetByDeviceAsync_ShouldReturnLogs()
    {
        using var context = CreateContext();
        var repo = new LogRepository(context);
        var deviceId = Guid.NewGuid();
        var log1 = new DeviceLog { DeviceId = deviceId, Level = "INFO", Message = "Log 1" };
        var log2 = new DeviceLog { DeviceId = deviceId, Level = "ERROR", Message = "Log 2" };
        await repo.CreateAsync(log1);
        await repo.CreateAsync(log2);

        var result = await repo.GetByDeviceAsync(deviceId);

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetUnsyncdAsync_ShouldReturnUnsyncdLogs()
    {
        using var context = CreateContext();
        var repo = new LogRepository(context);
        var log1 = new DeviceLog { DeviceId = Guid.NewGuid(), Level = "INFO", Message = "Log 1", Synced = false };
        var log2 = new DeviceLog { DeviceId = Guid.NewGuid(), Level = "INFO", Message = "Log 2", Synced = true };
        await repo.CreateAsync(log1);
        await repo.CreateAsync(log2);

        var result = await repo.GetUnsyncdAsync();

        result.Should().HaveCount(1);
        result[0].Message.Should().Be("Log 1");
    }

    [Fact]
    public async Task MarkSyncedAsync_ShouldMarkLogsAsSynced()
    {
        using var context = CreateContext();
        var repo = new LogRepository(context);
        var log = new DeviceLog { DeviceId = Guid.NewGuid(), Level = "INFO", Message = "Test", Synced = false };
        await repo.CreateAsync(log);

        await repo.MarkSyncedAsync([log.Id]);

        var unsynced = await repo.GetUnsyncdAsync();
        unsynced.Should().BeEmpty();
    }
}
