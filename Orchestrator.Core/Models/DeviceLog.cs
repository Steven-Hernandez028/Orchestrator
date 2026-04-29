namespace Orchestrator.Core.Models;

public class DeviceLog
{
    public Guid Id { get; set; }
    public Guid DeviceId { get; set; }
    public Guid? ScriptId { get; set; }
    public string? StepId { get; set; }
    public string Level { get; set; } = "INFO";
    public string Message { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public bool Synced { get; set; } = false;
}
