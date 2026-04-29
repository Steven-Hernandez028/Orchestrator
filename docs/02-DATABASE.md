# Database Schema & Models

## Overview

SQLite + EF Core for 20-device scale. Models store devices, scripts, logs, metrics, and assignments. Room DB on Android mirrors local state.

## Entity Relationship Diagram

```
┌─────────────┐
│   Device    │ ──────┐
├─────────────┤       │
│ Id (PK)     │       │
│ Serial*     │       │
│ Name        │       │
│ State       │       │
│ LastSeen    │       │
│ CurrentId*  │       │
└─────────────┘       │
                      │ 1:N
                      │
┌─────────────────┐   │   ┌──────────────┐
│ ScriptAssignment│◄──┴───┤   Script     │
├─────────────────┤       ├──────────────┤
│ Id (PK)         │       │ Id (PK)      │
│ DeviceId (FK)   │       │ Name         │
│ ScriptId (FK)   │       │ Version      │
│ AssignedAt      │       │ JsonDef      │
│ CompletedAt     │       │ CreatedAt    │
└─────────────────┘       │ UpdatedAt    │
                          └──────────────┘
                                 △
                                 │ 1:N
                          ┌──────┴────────┐
                          │               │
                    ┌──────────────┐ ┌──────────────┐
                    │ DeviceLog    │ │ DeviceMetric │
                    ├──────────────┤ ├──────────────┤
                    │ Id (PK)      │ │ Id (PK)      │
                    │ DeviceId (FK)│ │ DeviceId(FK) │
                    │ ScriptId(FK) │ │ Timestamp    │
                    │ StepId       │ │ CpuPercent   │
                    │ Level        │ │ RamUsedMb    │
                    │ Message      │ │ BatteryPct   │
                    │ Timestamp    │ │ NetworkRx    │
                    │ Synced       │ │ Synced       │
                    └──────────────┘ └──────────────┘
```

## Tables

### Device
Core entity. Tracks phone state, last contact, current script.

```csharp
public class Device
{
    public string Id { get; set; }                    // UUID
    public string DeviceSerial { get; set; }          // Unique (S8 serial) — Index
    public string FriendlyName { get; set; }
    public string AndroidVersion { get; set; }        // "9", "10", etc
    public DeviceState State { get; set; }            // IDLE, EXECUTING, PAUSED, OFFLINE
    public DateTime LastSeen { get; set; }
    public string CurrentScriptId { get; set; }       // Nullable, FK to Script
    public DateTime CreatedAt { get; set; }
    
    // Navigation
    public ICollection<DeviceLog> Logs { get; set; }
    public ICollection<DeviceMetric> Metrics { get; set; }
    public ICollection<DeviceScriptAssignment> ScriptAssignments { get; set; }
}
```

**DeviceState enum:** `IDLE`, `EXECUTING`, `PAUSED`, `OFFLINE`, `ERROR`

**Index:** `(DeviceSerial)` unique for fast lookup by serial number

---

### Script
Immutable by version. Backend creates v1, updates create v2, v3... Version returned in assign/broadcast commands.

```csharp
public class Script
{
    public string Id { get; set; }                    // UUID
    public string Name { get; set; }
    public int Version { get; set; }                  // Auto-increment on update
    public string JsonDefinition { get; set; }        // Full script JSON blob
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    
    // Navigation
    public ICollection<DeviceLog> Logs { get; set; }
    public ICollection<DeviceScriptAssignment> Assignments { get; set; }
}
```

**JsonDefinition format:**
```json
{
  "scriptId": "uuid",
  "name": "Daily Test",
  "version": 1,
  "loopCount": -1,
  "steps": [
    {
      "stepId": "s1",
      "type": "LAUNCH_APP",
      "params": { "packageName": "com.example.app" },
      "timeoutMs": 5000,
      "onFailure": "RETRY",
      "retryCount": 3
    }
  ]
}
```

---

### DeviceLog
Telemetry from script execution. Buffered on device if offline, synced to backend on reconnect.

```csharp
public class DeviceLog
{
    public string Id { get; set; }                    // UUID
    public string DeviceId { get; set; }              // FK to Device — Index
    public string ScriptId { get; set; }              // FK to Script
    public string StepId { get; set; }                // Step identifier within script
    public string Level { get; set; }                 // "INFO", "WARN", "ERROR"
    public string Message { get; set; }               // Log message (1000 char max)
    public DateTime Timestamp { get; set; }           // When written — Index
    public bool Synced { get; set; } = false;         // Offline buffering flag
    
    // Navigation
    public Device Device { get; set; }
    public Script Script { get; set; }
}
```

**Index:** `(DeviceId, Timestamp)` for fast range queries (time window, single device)

**Synced flag:** False while offline + buffered in Room DB. True after POST /api/logs/batch uploads.

**Data flow:**
```
Step executes (device online)
  → LogRepository.WriteAsync()
  → MQTT publish to orchestrator/devices/{id}/logs
  → Synced = true

Step executes (device offline)
  → Room DB: LogEntity with synced=false
  → MQTT unavailable
  → (backend down)
  → On reconnect: LogUploadService batches room logs
  → POST /api/logs/batch
  → Backend creates DeviceLog rows
  → Synced = true in Room DB
```

---

### DeviceMetric
Periodic telemetry: CPU, RAM, battery, network. Sampled every 30s.

```csharp
public class DeviceMetric
{
    public string Id { get; set; }                    // UUID
    public string DeviceId { get; set; }              // FK to Device — Index
    public DateTime Timestamp { get; set; }           // Measurement time — Index
    public double CpuPercent { get; set; }            // 0–100
    public int RamUsedMb { get; set; }
    public int BatteryPercent { get; set; }           // 0–100
    public long NetworkRxBytes { get; set; }          // Cumulative
    public long NetworkTxBytes { get; set; }
    public bool Synced { get; set; } = false;         // Offline buffering
    
    // Navigation
    public Device Device { get; set; }
}
```

**Index:** `(DeviceId, Timestamp)` for analytics queries

**Sample calculation:**
- 20 devices × 30s interval = 2 samples/min
- 2 samples/min × 1440 min/day = 2880 rows/day
- 30 days = ~86,400 rows — SQLite handles easily

---

### DeviceScriptAssignment
Links device→script for tracking execution lifecycle.

```csharp
public class DeviceScriptAssignment
{
    public string Id { get; set; }                    // UUID
    public string DeviceId { get; set; }              // FK to Device
    public string ScriptId { get; set; }              // FK to Script
    public DateTime AssignedAt { get; set; }
    public DateTime? CompletedAt { get; set; }        // Nullable until finished
    public string Status { get; set; }                // "PENDING", "RUNNING", "COMPLETED", "FAILED"
    
    // Navigation
    public Device Device { get; set; }
    public Script Script { get; set; }
}
```

**Used for:**
- Tracking which scripts ran on which devices (audit trail)
- Detecting orphaned assignments (assigned but device never ACKed)
- Calculating completion time for metrics dashboards

---

## Database Context (EF Core)

```csharp
public class OrchestratorDbContext : DbContext
{
    public DbSet<Device> Devices { get; set; }
    public DbSet<Script> Scripts { get; set; }
    public DbSet<DeviceLog> DeviceLogs { get; set; }
    public DbSet<DeviceMetric> DeviceMetrics { get; set; }
    public DbSet<DeviceScriptAssignment> DeviceScriptAssignments { get; set; }

    public OrchestratorDbContext(DbContextOptions<OrchestratorDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Device: serial unique
        modelBuilder.Entity<Device>()
            .HasIndex(d => d.DeviceSerial)
            .IsUnique();

        // DeviceLog: (DeviceId, Timestamp)
        modelBuilder.Entity<DeviceLog>()
            .HasIndex(l => new { l.DeviceId, l.Timestamp });

        // DeviceMetric: (DeviceId, Timestamp)
        modelBuilder.Entity<DeviceMetric>()
            .HasIndex(m => new { m.DeviceId, m.Timestamp });

        // Foreign keys
        modelBuilder.Entity<DeviceLog>()
            .HasOne(l => l.Device)
            .WithMany(d => d.Logs)
            .HasForeignKey(l => l.DeviceId);

        modelBuilder.Entity<DeviceLog>()
            .HasOne(l => l.Script)
            .WithMany(s => s.Logs)
            .HasForeignKey(l => l.ScriptId);

        modelBuilder.Entity<DeviceMetric>()
            .HasOne(m => m.Device)
            .WithMany(d => d.Metrics)
            .HasForeignKey(m => m.DeviceId);

        modelBuilder.Entity<DeviceScriptAssignment>()
            .HasOne(a => a.Device)
            .WithMany(d => d.ScriptAssignments)
            .HasForeignKey(a => a.DeviceId);

        modelBuilder.Entity<DeviceScriptAssignment>()
            .HasOne(a => a.Script)
            .WithMany(s => s.Assignments)
            .HasForeignKey(a => a.ScriptId);
    }
}
```

---

## Repositories

### IDeviceRepository
```csharp
public interface IDeviceRepository
{
    Task<Device> GetByIdAsync(string id);
    Task<Device> GetBySerialAsync(string serial);
    Task<IEnumerable<Device>> GetAllAsync();
    Task<Device> CreateAsync(Device device);
    Task<Device> UpdateAsync(Device device);
    Task DeleteAsync(string id);
}
```

Key operation: `GetBySerialAsync()` for device check-in on /api/devices/register.

### IScriptRepository
```csharp
public interface IScriptRepository
{
    Task<Script> GetByIdAsync(string id);
    Task<IEnumerable<Script>> GetAllAsync();
    Task<Script> CreateAsync(Script script);    // Sets Version = 1
    Task<Script> UpdateAsync(Script script);    // Increments Version
    Task DeleteAsync(string id);
}
```

**Version strategy:** On PUT, repo loads current version, increments, saves. Clients receive new version in response.

### ILogRepository
```csharp
public interface ILogRepository
{
    Task<DeviceLog> CreateAsync(DeviceLog log);
    Task<IEnumerable<DeviceLog>> GetByDeviceAsync(
        string deviceId, DateTime from, DateTime to, int page = 1);
    Task<IEnumerable<DeviceLog>> GetUnsyncdAsync();      // Offline buffering
    Task MarkSyncedAsync(IEnumerable<string> logIds);   // After batch upload
}
```

**GetUnsyncdAsync:** For backend to query logs still pending sync from devices (shouldn't happen often, used for diagnostics).

### IMetricRepository
```csharp
public interface IMetricRepository
{
    Task<DeviceMetric> CreateAsync(DeviceMetric metric);
    Task<DeviceMetric> GetLatestByDeviceAsync(string deviceId);
    Task<IEnumerable<DeviceMetric>> GetByDeviceAsync(
        string deviceId, DateTime from, DateTime to, int page = 1);
}
```

---

## Migrations & Setup

**Development:**
```bash
cd Orchestrator.Api
dotnet ef migrations add InitialCreate --project ../Orchestrator.Infrastructure
dotnet ef database update
```

**EnsureCreated in Program.cs:** For SQLite dev, auto-creates schema on startup.

```csharp
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<OrchestratorDbContext>();
    db.Database.EnsureCreated();
}
```

**Testing:** CustomWebApplicationFactory replaces DbContext with InMemory, no migrations needed.

---

## Data Retention Policy

### Backend
- **Logs:** Keep 30 days, delete older. Cron job or `DataRetentionService` (IHostedService) runs nightly.
- **Metrics:** Keep 30 days.
- **Devices:** Never auto-delete (manual removal).
- **Scripts:** Never auto-delete (versionable, audit trail).
- **Assignments:** Keep indefinitely (audit).

**Query for cleanup:**
```csharp
var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);
var oldLogs = context.DeviceLogs.Where(l => l.Timestamp < thirtyDaysAgo);
context.DeviceLogs.RemoveRange(oldLogs);
await context.SaveChangesAsync();
```

### Android (Room DB)
- **Scripts:** Keep current + last 2 versions.
- **Logs:** Batch-delete every 7 days OR when count > 1000.
- **Metrics:** Aggressive: keep last 24h only (disk space on phone is limited).

---

## Scalability (Future)

**Current (20 devices):**
- SQLite in /data/orchestrator.db
- ~86k rows/month (logs + metrics)
- No indexing stress

**100+ devices (Year 2):**
- Migrate to PostgreSQL
- Separate read replica for analytics
- Partition DeviceLogs by time (monthly)
- Archive to S3 after 90 days
