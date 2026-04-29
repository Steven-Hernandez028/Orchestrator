namespace Orchestrator.Application.DTOs;

public class AssignScriptRequest
{
    public Guid DeviceId { get; set; }
    public Guid ScriptId { get; set; }
}

public class BroadcastScriptRequest
{
    public Guid ScriptId { get; set; }
}

public class DeviceCommandRequest
{
    public Guid DeviceId { get; set; }
}
