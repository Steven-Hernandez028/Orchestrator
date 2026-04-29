using Orchestrator.Core.Enums;

namespace Orchestrator.Core.Models;

public class Device
{
    public Guid Id { get; set; }
    public string DeviceSerial { get; set; } = string.Empty;
    public string FriendlyName { get; set; } = string.Empty;
    public string AndroidVersion { get; set; } = string.Empty;
    public DateTime LastSeen { get; set; }
    public DeviceState State { get; set; }
    public Guid? CurrentScriptId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
