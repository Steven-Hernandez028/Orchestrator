namespace Orchestrator.Application.DTOs;

public class DeviceDto
{
    public Guid Id { get; set; }
    public string DeviceSerial { get; set; } = string.Empty;
    public string FriendlyName { get; set; } = string.Empty;
    public string AndroidVersion { get; set; } = string.Empty;
    public DateTime LastSeen { get; set; }
    public string State { get; set; } = string.Empty;
    public Guid? CurrentScriptId { get; set; }
}
