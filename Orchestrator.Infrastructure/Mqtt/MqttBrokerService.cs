using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Orchestrator.Infrastructure.Mqtt;

public class MqttBrokerService : IHostedService
{
    private readonly ILogger<MqttBrokerService> _logger;

    public MqttBrokerService(ILogger<MqttBrokerService> logger)
    {
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("MQTT Broker: Expected external Mosquitto on port 1883");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
