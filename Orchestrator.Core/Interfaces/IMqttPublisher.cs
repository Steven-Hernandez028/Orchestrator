namespace Orchestrator.Core.Interfaces;

public interface IMqttPublisher
{
    Task PublishAsync(string topic, string payload, int qos = 1);
    Task PublishAsync(string topic, byte[] payload, int qos = 1);
}
