namespace Orchestrator.Core.Models;

public class DeviceMetric
{
    public Guid Id { get; set; }
    public Guid DeviceId { get; set; }
    public float CpuPercent { get; set; }
    public int RamUsedMb { get; set; }
    public int BatteryPercent { get; set; }
    public long NetworkRxBytes { get; set; }
    public long NetworkTxBytes { get; set; }
    public DateTime Timestamp { get; set; }
    public bool Synced { get; set; } = false;
}
