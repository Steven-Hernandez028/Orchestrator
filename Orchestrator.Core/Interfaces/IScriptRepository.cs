using Orchestrator.Core.Models;

namespace Orchestrator.Core.Interfaces;

public interface IScriptRepository
{
    Task<Script?> GetByIdAsync(Guid id);
    Task<List<Script>> GetAllAsync();
    Task<Script> CreateAsync(Script script);
    Task<Script> UpdateAsync(Script script);
    Task<bool> DeleteAsync(Guid id);
}
