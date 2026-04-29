using Microsoft.EntityFrameworkCore;
using Orchestrator.Core.Interfaces;
using Orchestrator.Core.Models;

namespace Orchestrator.Infrastructure.Data.Repositories;

public class LogRepository : ILogRepository
{
    private readonly OrchestratorDbContext _context;

    public LogRepository(OrchestratorDbContext context)
    {
        _context = context;
    }

    public async Task<DeviceLog> CreateAsync(DeviceLog log)
    {
        log.Id = Guid.NewGuid();
        log.Timestamp = DateTime.UtcNow;
        _context.DeviceLogs.Add(log);
        await _context.SaveChangesAsync();
        return log;
    }

    public async Task<List<DeviceLog>> GetByDeviceAsync(Guid deviceId, int limit = 1000)
    {
        return await _context.DeviceLogs
            .Where(l => l.DeviceId == deviceId)
            .OrderByDescending(l => l.Timestamp)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<List<DeviceLog>> GetUnsyncdAsync(int limit = 500)
    {
        return await _context.DeviceLogs
            .Where(l => !l.Synced)
            .OrderBy(l => l.Timestamp)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<bool> MarkSyncedAsync(List<Guid> ids)
    {
        var logs = await _context.DeviceLogs
            .Where(l => ids.Contains(l.Id))
            .ToListAsync();

        foreach (var log in logs)
            log.Synced = true;

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<List<DeviceLog>> GetByDeviceAndTimeAsync(Guid deviceId, DateTime from, DateTime to)
    {
        return await _context.DeviceLogs
            .Where(l => l.DeviceId == deviceId && l.Timestamp >= from && l.Timestamp <= to)
            .OrderByDescending(l => l.Timestamp)
            .ToListAsync();
    }

    public async Task DeleteOlderThanAsync(DateTime before)
    {
        var oldLogs = await _context.DeviceLogs
            .Where(l => l.Timestamp < before)
            .ToListAsync();

        _context.DeviceLogs.RemoveRange(oldLogs);
        await _context.SaveChangesAsync();
    }
}
