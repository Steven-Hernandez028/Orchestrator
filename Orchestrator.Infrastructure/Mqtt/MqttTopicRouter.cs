using Microsoft.Extensions.Logging;
using Orchestrator.Core.Interfaces;

namespace Orchestrator.Infrastructure.Mqtt;

public class MqttTopicRouter
{
    private readonly IOrchestrationService _orchestrationService;
    private readonly ILogger<MqttTopicRouter> _logger;

    public MqttTopicRouter(IOrchestrationService orchestrationService, ILogger<MqttTopicRouter> logger)
    {
        _orchestrationService = orchestrationService;
        _logger = logger;
    }

    public async Task RouteAsync(string topic, byte[] payload)
    {
        var payloadStr = System.Text.Encoding.UTF8.GetString(payload);
        _logger.LogDebug("Received message on topic {Topic}: {Payload}", topic, payloadStr);

        var parts = topic.Split('/');
        if (parts.Length < 4)
        {
            _logger.LogWarning("Invalid topic format: {Topic}", topic);
            return;
        }

        var deviceId = parts[2];
        var messageType = parts[3];

        if (!Guid.TryParse(deviceId, out var parsedDeviceId))
        {
            _logger.LogWarning("Invalid device ID in topic: {DeviceId}", deviceId);
            return;
        }

        try
        {
            switch (messageType)
            {
                case "status":
                    await _orchestrationService.HandleDeviceStatusAsync(parsedDeviceId, payloadStr);
                    break;
                case "logs":
                    _logger.LogDebug("Log message from device {DeviceId}", parsedDeviceId);
                    break;
                case "metrics":
                    _logger.LogDebug("Metric message from device {DeviceId}", parsedDeviceId);
                    break;
                case "ack":
                    var commandId = payloadStr;
                    await _orchestrationService.HandleAckAsync(commandId);
                    break;
                default:
                    _logger.LogWarning("Unknown message type: {MessageType}", messageType);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error routing message on topic {Topic}", topic);
        }
    }
}
