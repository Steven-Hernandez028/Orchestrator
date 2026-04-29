using Orchestrator.Core.Models;

namespace Orchestrator.Core.Interfaces;

public interface IDeviceRepository
{
    Task<Device?> GetByIdAsync(Guid id);
    Task<Device?> GetBySerialAsync(string serial);
    Task<List<Device>> GetAllAsync();
    Task<Device> CreateAsync(Device device);
    Task<Device> UpdateAsync(Device device);
    Task<bool> DeleteAsync(Guid id);
}
