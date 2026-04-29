namespace Orchestrator.Core.Models;

public class DeviceScriptAssignment
{
    public Guid Id { get; set; }
    public Guid DeviceId { get; set; }
    public Guid ScriptId { get; set; }
    public DateTime AssignedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
