using Orchestrator.Core.Models;

namespace Orchestrator.Core.Interfaces;

public interface IMetricRepository
{
    Task<DeviceMetric> CreateAsync(DeviceMetric metric);
    Task<List<DeviceMetric>> GetByDeviceAsync(Guid deviceId, int limit = 1000);
    Task<DeviceMetric?> GetLatestByDeviceAsync(Guid deviceId);
    Task<List<DeviceMetric>> GetByDeviceAndTimeAsync(Guid deviceId, DateTime from, DateTime to);
    Task<List<DeviceMetric>> GetUnsyncdAsync(int limit = 500);
    Task<bool> MarkSyncedAsync(List<Guid> ids);
    Task DeleteOlderThanAsync(DateTime before);
}
