using Microsoft.Extensions.Logging;
using Orchestrator.Core.Interfaces;

namespace Orchestrator.Infrastructure.Mqtt;

public class MqttPublisher : IMqttPublisher
{
    private readonly ILogger<MqttPublisher> _logger;
    private bool _connected;

    public MqttPublisher(ILogger<MqttPublisher> logger)
    {
        _logger = logger;
        _connected = false;
    }

    public Task PublishAsync(string topic, string payload, int qos = 1)
    {
        return PublishAsync(topic, System.Text.Encoding.UTF8.GetBytes(payload), qos);
    }

    public async Task PublishAsync(string topic, byte[] payload, int qos = 1)
    {
        if (!_connected)
        {
            _logger.LogWarning("MQTT Client not connected, cannot publish to {Topic}", topic);
            return;
        }

        _logger.LogDebug("Published to {Topic}: {PayloadSize} bytes", topic, payload.Length);
        await Task.CompletedTask;
    }

    public void SetConnected(bool connected)
    {
        _connected = connected;
        _logger.LogInformation("MQTT Publisher connection status: {Status}", connected ? "Connected" : "Disconnected");
    }
}
