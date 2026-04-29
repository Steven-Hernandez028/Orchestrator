using Microsoft.EntityFrameworkCore;
using Orchestrator.Core.Enums;
using Orchestrator.Core.Models;

namespace Orchestrator.Infrastructure.Data;

public class OrchestratorDbContext : DbContext
{
    public OrchestratorDbContext(DbContextOptions<OrchestratorDbContext> options) : base(options) { }

    public DbSet<Device> Devices { get; set; }
    public DbSet<Script> Scripts { get; set; }
    public DbSet<DeviceLog> DeviceLogs { get; set; }
    public DbSet<DeviceMetric> DeviceMetrics { get; set; }
    public DbSet<DeviceScriptAssignment> DeviceScriptAssignments { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Device
        modelBuilder.Entity<Device>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.DeviceSerial).IsRequired();
            e.HasIndex(x => x.DeviceSerial).IsUnique();
            e.Property(x => x.State).HasConversion<string>();
        });

        // Script
        modelBuilder.Entity<Script>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).IsRequired();
            e.Property(x => x.JsonDefinition).IsRequired();
        });

        // DeviceLog
        modelBuilder.Entity<DeviceLog>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Message).IsRequired();
            e.HasIndex(x => new { x.DeviceId, x.Timestamp });
        });

        // DeviceMetric
        modelBuilder.Entity<DeviceMetric>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.DeviceId, x.Timestamp });
        });

        // DeviceScriptAssignment
        modelBuilder.Entity<DeviceScriptAssignment>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.DeviceId, x.ScriptId });
        });
    }
}
