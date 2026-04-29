using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Orchestrator.Application.Services;
using Orchestrator.Core.Enums;
using Orchestrator.Core.Interfaces;
using Orchestrator.Core.Models;
using Xunit;

namespace Orchestrator.UnitTests.Services;

public class OrchestrationServiceTests
{
    private readonly Mock<IDeviceRepository> _mockDeviceRepo;
    private readonly Mock<IScriptRepository> _mockScriptRepo;
    private readonly Mock<IMqttPublisher> _mockPublisher;
    private readonly Mock<ILogger<OrchestrationService>> _mockLogger;
    private readonly OrchestrationService _service;

    public OrchestrationServiceTests()
    {
        _mockDeviceRepo = new Mock<IDeviceRepository>();
        _mockScriptRepo = new Mock<IScriptRepository>();
        _mockPublisher = new Mock<IMqttPublisher>();
        _mockLogger = new Mock<ILogger<OrchestrationService>>();

        _service = new OrchestrationService(
            _mockDeviceRepo.Object,
            _mockScriptRepo.Object,
            _mockPublisher.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task AssignScriptAsync_WithValidIds_ShouldPublishCommand()
    {
        var deviceId = Guid.NewGuid();
        var scriptId = Guid.NewGuid();
        var device = new Device { Id = deviceId, DeviceSerial = "ABC123", State = DeviceState.Online };
        var script = new Script { Id = scriptId, Name = "Test", JsonDefinition = "{}" };

        _mockDeviceRepo.Setup(r => r.GetByIdAsync(deviceId))
            .ReturnsAsync(device);
        _mockScriptRepo.Setup(r => r.GetByIdAsync(scriptId))
            .ReturnsAsync(script);
        _mockPublisher.Setup(p => p.PublishAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
            .Returns(Task.CompletedTask);
        _mockDeviceRepo.Setup(r => r.UpdateAsync(It.IsAny<Device>()))
            .ReturnsAsync((Device d) => d);

        await _service.AssignScriptAsync(deviceId, scriptId);

        _mockPublisher.Verify(p => p.PublishAsync(
            It.Is<string>(t => t.Contains(deviceId.ToString())),
            It.IsAny<string>(),
            1), Times.Once);
    }

    [Fact]
    public async Task AssignScriptAsync_WithInvalidDevice_ShouldNotPublish()
    {
        var deviceId = Guid.NewGuid();
        var scriptId = Guid.NewGuid();

        _mockDeviceRepo.Setup(r => r.GetByIdAsync(deviceId))
            .ReturnsAsync((Device?)null);

        await _service.AssignScriptAsync(deviceId, scriptId);

        _mockPublisher.Verify(p => p.PublishAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task BroadcastScriptAsync_ShouldPublishToAllDevices()
    {
        var scriptId = Guid.NewGuid();
        var script = new Script { Id = scriptId, Name = "Test", JsonDefinition = "{}" };

        _mockScriptRepo.Setup(r => r.GetByIdAsync(scriptId))
            .ReturnsAsync(script);
        _mockPublisher.Setup(p => p.PublishAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
            .Returns(Task.CompletedTask);

        await _service.BroadcastScriptAsync(scriptId);

        _mockPublisher.Verify(p => p.PublishAsync(
            It.Is<string>(t => t.Contains("broadcast")),
            It.IsAny<string>(),
            1), Times.Once);
    }

    [Fact]
    public async Task PauseExecutionAsync_ShouldPublishPauseCommand()
    {
        var deviceId = Guid.NewGuid();

        _mockPublisher.Setup(p => p.PublishAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
            .Returns(Task.CompletedTask);

        await _service.PauseExecutionAsync(deviceId);

        _mockPublisher.Verify(p => p.PublishAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            1), Times.Once);
    }

    [Fact]
    public async Task HandleDeviceStatusAsync_ShouldUpdateLastSeen()
    {
        var deviceId = Guid.NewGuid();
        var device = new Device { Id = deviceId, DeviceSerial = "ABC", State = DeviceState.Online };

        _mockDeviceRepo.Setup(r => r.GetByIdAsync(deviceId))
            .ReturnsAsync(device);
        _mockDeviceRepo.Setup(r => r.UpdateAsync(It.IsAny<Device>()))
            .ReturnsAsync((Device d) => d);

        var beforeUpdate = DateTime.UtcNow;
        await _service.HandleDeviceStatusAsync(deviceId, "{}");
        var afterUpdate = DateTime.UtcNow;

        _mockDeviceRepo.Verify(r => r.UpdateAsync(It.Is<Device>(d =>
            d.LastSeen >= beforeUpdate && d.LastSeen <= afterUpdate)), Times.Once);
    }
}
