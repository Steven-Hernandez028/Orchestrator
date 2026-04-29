using Orchestrator.Core.Enums;

namespace Orchestrator.Core.Models;

public class CommandEnvelope
{
    public string CommandId { get; set; } = Guid.NewGuid().ToString();
    public CommandType Type { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string Payload { get; set; } = string.Empty;
    public bool RequiresAck { get; set; }
}
