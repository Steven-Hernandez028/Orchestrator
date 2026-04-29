using Orchestrator.Core.Models;

namespace Orchestrator.Core.Interfaces;

public interface ILogRepository
{
    Task<DeviceLog> CreateAsync(DeviceLog log);
    Task<List<DeviceLog>> GetByDeviceAsync(Guid deviceId, int limit = 1000);
    Task<List<DeviceLog>> GetUnsyncdAsync(int limit = 500);
    Task<bool> MarkSyncedAsync(List<Guid> ids);
    Task<List<DeviceLog>> GetByDeviceAndTimeAsync(Guid deviceId, DateTime from, DateTime to);
    Task DeleteOlderThanAsync(DateTime before);
}
