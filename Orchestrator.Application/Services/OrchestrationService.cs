using System.Text.Json;
using Microsoft.Extensions.Logging;
using Orchestrator.Core.Enums;
using Orchestrator.Core.Interfaces;
using Orchestrator.Core.Models;

namespace Orchestrator.Application.Services;

public class OrchestrationService : IOrchestrationService
{
    private readonly IDeviceRepository _deviceRepository;
    private readonly IScriptRepository _scriptRepository;
    private readonly IMqttPublisher _mqttPublisher;
    private readonly ILogger<OrchestrationService> _logger;

    public OrchestrationService(
        IDeviceRepository deviceRepository,
        IScriptRepository scriptRepository,
        IMqttPublisher mqttPublisher,
        ILogger<OrchestrationService> logger)
    {
        _deviceRepository = deviceRepository;
        _scriptRepository = scriptRepository;
        _mqttPublisher = mqttPublisher;
        _logger = logger;
    }

    public async Task AssignScriptAsync(Guid deviceId, Guid scriptId)
    {
        var device = await _deviceRepository.GetByIdAsync(deviceId);
        if (device == null)
        {
            _logger.LogWarning("Device {DeviceId} not found", deviceId);
            return;
        }

        var script = await _scriptRepository.GetByIdAsync(scriptId);
        if (script == null)
        {
            _logger.LogWarning("Script {ScriptId} not found", scriptId);
            return;
        }

        var command = new CommandEnvelope
        {
            CommandId = Guid.NewGuid().ToString(),
            Type = CommandType.AssignScript,
            Payload = script.JsonDefinition,
            RequiresAck = true
        };

        var topic = $"orchestrator/devices/{deviceId}/commands";
        var json = JsonSerializer.Serialize(command);
        await _mqttPublisher.PublishAsync(topic, json, qos: 1);

        device.CurrentScriptId = scriptId;
        device.State = DeviceState.Executing;
        await _deviceRepository.UpdateAsync(device);

        _logger.LogInformation("Assigned script {ScriptId} to device {DeviceId}", scriptId, deviceId);
    }

    public async Task BroadcastScriptAsync(Guid scriptId)
    {
        var script = await _scriptRepository.GetByIdAsync(scriptId);
        if (script == null)
        {
            _logger.LogWarning("Script {ScriptId} not found", scriptId);
            return;
        }

        var command = new CommandEnvelope
        {
            CommandId = Guid.NewGuid().ToString(),
            Type = CommandType.AssignScript,
            Payload = script.JsonDefinition,
            RequiresAck = true
        };

        var topic = "orchestrator/broadcast/commands";
        var json = JsonSerializer.Serialize(command);
        await _mqttPublisher.PublishAsync(topic, json, qos: 1);

        _logger.LogInformation("Broadcasted script {ScriptId} to all devices", scriptId);
    }

    public async Task PauseExecutionAsync(Guid deviceId)
    {
        var command = new CommandEnvelope
        {
            CommandId = Guid.NewGuid().ToString(),
            Type = CommandType.PauseExecution,
            RequiresAck = true
        };

        var topic = $"orchestrator/devices/{deviceId}/commands";
        var json = JsonSerializer.Serialize(command);
        await _mqttPublisher.PublishAsync(topic, json, qos: 1);

        _logger.LogInformation("Pause command sent to device {DeviceId}", deviceId);
    }

    public async Task ResumeExecutionAsync(Guid deviceId)
    {
        var command = new CommandEnvelope
        {
            CommandId = Guid.NewGuid().ToString(),
            Type = CommandType.ResumeExecution,
            RequiresAck = true
        };

        var topic = $"orchestrator/devices/{deviceId}/commands";
        var json = JsonSerializer.Serialize(command);
        await _mqttPublisher.PublishAsync(topic, json, qos: 1);

        _logger.LogInformation("Resume command sent to device {DeviceId}", deviceId);
    }

    public async Task AbortExecutionAsync(Guid deviceId)
    {
        var command = new CommandEnvelope
        {
            CommandId = Guid.NewGuid().ToString(),
            Type = CommandType.AbortExecution,
            RequiresAck = true
        };

        var topic = $"orchestrator/devices/{deviceId}/commands";
        var json = JsonSerializer.Serialize(command);
        await _mqttPublisher.PublishAsync(topic, json, qos: 1);

        var device = await _deviceRepository.GetByIdAsync(deviceId);
        if (device != null)
        {
            device.State = DeviceState.Offline;
            await _deviceRepository.UpdateAsync(device);
        }

        _logger.LogInformation("Abort command sent to device {DeviceId}", deviceId);
    }

    public async Task HandleDeviceStatusAsync(Guid deviceId, string statusPayload)
    {
        var device = await _deviceRepository.GetByIdAsync(deviceId);
        if (device == null)
        {
            _logger.LogWarning("Device {DeviceId} not found for status update", deviceId);
            return;
        }

        device.LastSeen = DateTime.UtcNow;
        await _deviceRepository.UpdateAsync(device);

        _logger.LogDebug("Device {DeviceId} status updated: {Status}", deviceId, statusPayload);
    }

    public async Task HandleAckAsync(string commandId)
    {
        _logger.LogDebug("Received ACK for command {CommandId}", commandId);
        await Task.CompletedTask;
    }
}
