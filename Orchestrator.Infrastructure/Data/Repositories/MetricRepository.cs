using Microsoft.EntityFrameworkCore;
using Orchestrator.Core.Interfaces;
using Orchestrator.Core.Models;

namespace Orchestrator.Infrastructure.Data.Repositories;

public class MetricRepository : IMetricRepository
{
    private readonly OrchestratorDbContext _context;

    public MetricRepository(OrchestratorDbContext context)
    {
        _context = context;
    }

    public async Task<DeviceMetric> CreateAsync(DeviceMetric metric)
    {
        metric.Id = Guid.NewGuid();
        metric.Timestamp = DateTime.UtcNow;
        _context.DeviceMetrics.Add(metric);
        await _context.SaveChangesAsync();
        return metric;
    }

    public async Task<List<DeviceMetric>> GetByDeviceAsync(Guid deviceId, int limit = 1000)
    {
        return await _context.DeviceMetrics
            .Where(m => m.DeviceId == deviceId)
            .OrderByDescending(m => m.Timestamp)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<DeviceMetric?> GetLatestByDeviceAsync(Guid deviceId)
    {
        return await _context.DeviceMetrics
            .Where(m => m.DeviceId == deviceId)
            .OrderByDescending(m => m.Timestamp)
            .FirstOrDefaultAsync();
    }

    public async Task<List<DeviceMetric>> GetByDeviceAndTimeAsync(Guid deviceId, DateTime from, DateTime to)
    {
        return await _context.DeviceMetrics
            .Where(m => m.DeviceId == deviceId && m.Timestamp >= from && m.Timestamp <= to)
            .OrderByDescending(m => m.Timestamp)
            .ToListAsync();
    }

    public async Task<List<DeviceMetric>> GetUnsyncdAsync(int limit = 500)
    {
        return await _context.DeviceMetrics
            .Where(m => !m.Synced)
            .OrderBy(m => m.Timestamp)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<bool> MarkSyncedAsync(List<Guid> ids)
    {
        var metrics = await _context.DeviceMetrics
            .Where(m => ids.Contains(m.Id))
            .ToListAsync();

        foreach (var metric in metrics)
            metric.Synced = true;

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task DeleteOlderThanAsync(DateTime before)
    {
        var oldMetrics = await _context.DeviceMetrics
            .Where(m => m.Timestamp < before)
            .ToListAsync();

        _context.DeviceMetrics.RemoveRange(oldMetrics);
        await _context.SaveChangesAsync();
    }
}
