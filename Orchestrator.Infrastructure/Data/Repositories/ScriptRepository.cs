using Microsoft.EntityFrameworkCore;
using Orchestrator.Core.Interfaces;
using Orchestrator.Core.Models;

namespace Orchestrator.Infrastructure.Data.Repositories;

public class ScriptRepository : IScriptRepository
{
    private readonly OrchestratorDbContext _context;

    public ScriptRepository(OrchestratorDbContext context)
    {
        _context = context;
    }

    public async Task<Script?> GetByIdAsync(Guid id)
    {
        return await _context.Scripts.FindAsync(id);
    }

    public async Task<List<Script>> GetAllAsync()
    {
        return await _context.Scripts.ToListAsync();
    }

    public async Task<Script> CreateAsync(Script script)
    {
        script.Id = Guid.NewGuid();
        script.Version = 1;
        script.CreatedAt = DateTime.UtcNow;
        script.UpdatedAt = DateTime.UtcNow;
        _context.Scripts.Add(script);
        await _context.SaveChangesAsync();
        return script;
    }

    public async Task<Script> UpdateAsync(Script script)
    {
        script.Version++;
        script.UpdatedAt = DateTime.UtcNow;
        _context.Scripts.Update(script);
        await _context.SaveChangesAsync();
        return script;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var script = await _context.Scripts.FindAsync(id);
        if (script == null) return false;
        _context.Scripts.Remove(script);
        await _context.SaveChangesAsync();
        return true;
    }
}
