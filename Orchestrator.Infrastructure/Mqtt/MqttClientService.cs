using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Orchestrator.Infrastructure.Mqtt;

namespace Orchestrator.Infrastructure.Mqtt;

public class MqttClientService : IHostedService
{
    private readonly ILogger<MqttClientService> _logger;
    private readonly MqttPublisher _publisher;
    private readonly MqttTopicRouter _topicRouter;

    public MqttClientService(
        ILogger<MqttClientService> logger,
        MqttPublisher publisher,
        MqttTopicRouter topicRouter)
    {
        _logger = logger;
        _publisher = publisher;
        _topicRouter = topicRouter;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("MQTT Client: Expects external Mosquitto on port 1883");
        _publisher.SetConnected(true);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
