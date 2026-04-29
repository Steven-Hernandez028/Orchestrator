# Deployment & Setup Guide

## Development Environment Setup

### Prerequisites
- Windows 10/11 Pro (backend)
- .NET 10 SDK
- Visual Studio 2022 or VS Code
- Android Studio (for Android development)
- Kotlin 1.9+
- Git

### Backend Setup (Windows PC)

**1. Clone repository**
```bash
git clone https://github.com/yourusername/orchestrator.git
cd Orchestrator
```

**2. Install Mosquitto (MQTT Broker)**
```bash
# Windows (via Chocolatey)
choco install mosquitto

# Or download from: https://mosquitto.org/download/
```

**3. Configure Mosquitto**

Create `mosquitto.conf` (typically `C:\Program Files\mosquitto\mosquitto.conf`):
```
listener 1883
protocol mqtt
allow_anonymous true
persistence true
persistence_location C:\mosquitto\data\
log_dest file C:\mosquitto\log\mosquitto.log
log_dest stdout
```

**4. Start Mosquitto service**
```bash
# Windows Service (if installed)
net start mosquitto

# Or run manually
mosquitto -c "C:\Program Files\mosquitto\mosquitto.conf"
```

**5. Restore .NET dependencies & build**
```bash
cd Orchestrator
dotnet restore
dotnet build
```

**6. Run backend**
```bash
cd Orchestrator.Api
dotnet run --configuration Release
```

Expected output:
```
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:5000
```

**7. Verify API is working**
```bash
curl http://localhost:5000/api/devices
# Should return: []
```

---

### Android Agent Setup

**1. Open Android Studio**
```bash
# Navigate to android/ directory
android\app
```

**2. Configure build.gradle (Module: app)**

Verify dependencies:
```gradle
dependencies {
    implementation 'com.google.dagger:hilt-android:2.51'
    implementation 'androidx.room:room-runtime:2.6.1'
    implementation 'com.hivemq:hivemq-mqtt-client:1.3.3'
    implementation 'com.squareup.retrofit2:retrofit:2.11.0'
    // ... others
}
```

**3. Connect Android device (S8 Active) via USB**
```bash
adb devices
# Should show: R38M717AB0C        device
```

**4. Install APK to device**
```bash
# Build debug APK
./gradlew assembleDebug

# Install to connected device
adb install -r app/build/outputs/apk/debug/app-debug.apk

# Or directly from Android Studio: Run → Run 'app'
```

**5. Grant permissions**
- Settings → Apps → Orchestrator Agent
- Permissions:
  - Camera: Allow
  - Microphone: Allow
  - Storage: Allow
  - Accessibility Service: Manual grant
    - Settings → Accessibility → Orchestrator Agent → Enable

**6. Launch app**
```bash
adb shell am start -n com.orchestrator.agent/.ui.MainActivity
```

Expected behavior:
- App starts
- Auto-registers with backend (POST /api/devices/register)
- Shows status: "Ready for commands"

---

## Production Deployment

### Backend Deployment (Windows Server)

**Option 1: Windows Service**

Create PowerShell script (`deploy-service.ps1`):
```powershell
$ServiceName = "OrchestratorBackend"
$AppPath = "D:\Orchestrator\Orchestrator.Api"
$ExePath = "$AppPath\bin\Release\net10\Orchestrator.Api.exe"

# Stop existing service
Stop-Service -Name $ServiceName -ErrorAction SilentlyContinue

# Register new service (requires elevation)
New-Service -Name $ServiceName `
    -BinaryPathName $ExePath `
    -DisplayName "Orchestrator Backend" `
    -StartupType Automatic

# Start service
Start-Service -Name $ServiceName
```

**Option 2: Docker Container**

Create `Dockerfile`:
```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:10-windowsservercore-ltsc2022

WORKDIR /app
COPY Orchestrator.Api/bin/Release/net10 .

EXPOSE 5000
ENV ASPNETCORE_URLS=http://+:5000

ENTRYPOINT ["dotnet", "Orchestrator.Api.dll"]
```

Build & run:
```bash
docker build -t orchestrator-backend:1.0 .
docker run -d -p 5000:5000 \
  -v D:\orchestrator.db:C:\app\orchestrator.db \
  orchestrator-backend:1.0
```

### MQTT Broker (Mosquitto)

**Install as Windows Service:**
```bash
# Download MSI installer from: https://mosquitto.org/download/
# Run installer with admin privileges
```

**Or via Docker:**
```bash
docker run -d \
  -p 1883:1883 \
  -p 9001:9001 \
  -v mosquitto.conf:/mosquitto/config/mosquitto.conf \
  -v mosquitto-data:/mosquitto/data \
  eclipse-mosquitto:latest
```

**mosquitto.conf (production):**
```
listener 1883
protocol mqtt

# Authentication (optional but recommended)
allow_anonymous false
password_file /mosquitto/config/passwd.txt

# Persistence
persistence true
persistence_location /mosquitto/data/

# Logging
log_dest file /mosquitto/log/mosquitto.log
log_type all
log_timestamp true

# Security
max_connections 100
max_queued_messages 100
```

Create password file:
```bash
mosquitto_passwd -c /mosquitto/config/passwd.txt admin
# Enter password when prompted
```

**Firewall Rules:**
```powershell
# Windows Firewall
netsh advfirewall firewall add rule name="MQTT" dir=in action=allow protocol=tcp localport=1883
```

### Android Deployment

**1. Build signed APK (production)**
```bash
# Generate keystore (one-time)
keytool -genkey -v -keystore orchestrator.keystore \
  -keyalg RSA -keysize 2048 -validity 365

# Create signing config in build.gradle
signingConfigs {
    release {
        keyStore file('orchestrator.keystore')
        keyStorePassword 'your-password'
        keyAlias 'orchestrator'
        keyPassword 'your-password'
    }
}

buildTypes {
    release {
        signingConfig signingConfigs.release
    }
}

# Build release APK
./gradlew assembleRelease
```

**2. Deploy to 20 devices**

Script (`deploy-apk.ps1`):
```powershell
$apkPath = "app/build/outputs/apk/release/app-release.apk"

# Get list of connected devices
$devices = adb devices | Select-Object -Skip 1 | Where-Object { $_ -match '\s+device$' } | ForEach-Object { $_.Split()[0] }

Write-Host "Found $($devices.Count) devices"

foreach ($device in $devices) {
    Write-Host "Installing APK to $device..."
    adb -s $device install -r $apkPath
    
    # Launch app
    adb -s $device shell am start -n com.orchestrator.agent/.ui.MainActivity
    
    Start-Sleep -Seconds 2
}

Write-Host "Deployment complete"
```

Run:
```bash
powershell -ExecutionPolicy Bypass -File deploy-apk.ps1
```

---

## Configuration

### Backend (Program.cs)

**Environment variables:**
```bash
# .env file or system environment
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://0.0.0.0:5000
MQTT_HOST=localhost:1883
DATABASE_PATH=D:\orchestrator.db
```

**Read in Program.cs:**
```csharp
var mqttHost = Environment.GetEnvironmentVariable("MQTT_HOST") ?? "localhost:1883";
var dbPath = Environment.GetEnvironmentVariable("DATABASE_PATH") ?? "orchestrator.db";

builder.Services.AddDbContext<OrchestratorDbContext>(options =>
    options.UseSqlite($"Data Source={dbPath}"));
```

### Android (strings.xml)

```xml
<!-- res/values/strings.xml -->
<resources>
    <string name="app_name">Orchestrator Agent</string>
    <string name="mqtt_host">192.168.1.100</string>
    <string name="mqtt_port">1883</string>
    <string name="api_base_url">http://192.168.1.100:5000/api</string>
    <string name="device_name">Lab Phone {serial}</string>
</resources>
```

Read in code:
```kotlin
val mqttHost = context.getString(R.string.mqtt_host)
val mqttPort = context.getString(R.string.mqtt_port).toInt()
val apiBaseUrl = context.getString(R.string.api_base_url)
```

---

## Database Backups

### SQLite Backup

**Manual:**
```bash
# Copy database file
copy D:\orchestrator.db D:\backups\orchestrator_$(date +%Y%m%d_%H%M%S).db

# Or via EF Core CLI
dotnet ef database script --output backup.sql
```

**Automated (Task Scheduler):**

Create `backup-db.ps1`:
```powershell
$dbPath = "D:\orchestrator.db"
$backupDir = "D:\backups"
$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$backupPath = "$backupDir\orchestrator_$timestamp.db"

Copy-Item $dbPath $backupPath
Write-Host "Backup saved to $backupPath"

# Keep only last 30 days
Get-ChildItem $backupDir -Filter "orchestrator_*.db" | 
    Where-Object { $_.LastWriteTime -lt (Get-Date).AddDays(-30) } |
    Remove-Item
```

Schedule in Task Scheduler:
- Trigger: Daily at 2:00 AM
- Action: PowerShell -ExecutionPolicy Bypass -File D:\scripts\backup-db.ps1

---

## Monitoring & Troubleshooting

### Backend Health Check

```bash
# Check if API is responding
curl http://localhost:5000/api/devices

# Check database
curl http://localhost:5000/api/devices | jq '.[] | .deviceSerial'

# Check MQTT connectivity
mosquitto_sub -t '$SYS/broker/clients/connected' -h localhost
```

### Android Troubleshooting

**Logcat output:**
```bash
# Real-time logs
adb logcat com.orchestrator.agent:I

# Save to file
adb logcat com.orchestrator.agent:I > logs.txt

# Common errors:
# E/MqttManager: Connect timeout
#   → Check MQTT broker running
# E/MqttManager: Cannot resolve host
#   → Check MQTT_HOST in strings.xml
# E/ScriptEngine: StepException: No executor for CLICK
#   → Verify AccessibilityService enabled
```

**Device state:**
```bash
adb shell dumpsys package com.orchestrator.agent

# Check if service is running
adb shell ps | grep orchestrator
```

### MQTT Broker Monitoring

```bash
# Monitor all traffic
mosquitto_sub -t "#" -v

# Check client connections
mosquitto_sub -t '$SYS/broker/clients/connected'

# Check retained messages
mosquitto_sub -t '#' -R

# Check broker stats
mosquitto_sub -t '$SYS/broker/#'
```

---

## Performance Tuning

### SQLite Optimization

```sql
-- Enable WAL (Write-Ahead Logging)
PRAGMA journal_mode = WAL;

-- Increase cache size
PRAGMA cache_size = 5000;

-- Synchronous mode (balance safety + speed)
PRAGMA synchronous = NORMAL;

-- Create indexes on frequently queried columns
CREATE INDEX idx_device_logs_device_timestamp 
  ON DeviceLogs(DeviceId, Timestamp DESC);

CREATE INDEX idx_device_metrics_device_timestamp 
  ON DeviceMetrics(DeviceId, Timestamp DESC);
```

### MQTT Broker Tuning

In `mosquitto.conf`:
```
# Max packet size (increase if sending large scripts)
max_packet_size 1048576

# Queue settings
max_queued_messages 1000

# Memory limits
max_connections 100

# Performance
autosave_interval 3600  # Save persistence every hour
```

### Network Optimization

**Reduce payload size:**
- Compress script JSON
- Batch multiple messages per MQTT publish
- Use binary encoding for metrics (protobuf, msgpack)

**Example:** Batch 10 log entries before publishing
```kotlin
class LogUploadService {
    private val logBuffer = mutableListOf<LogEntity>()
    
    suspend fun addLog(log: LogEntity) {
        logBuffer.add(log)
        if (logBuffer.size >= 10) {
            flush()
        }
    }
    
    suspend fun flush() {
        if (logBuffer.isNotEmpty()) {
            // POST /api/logs/batch with all 10 logs
            mqttManager.publishBatch("orchestrator/devices/$id/logs", logBuffer)
            logBuffer.clear()
        }
    }
}
```

---

## Data Retention & Cleanup

### Automatic Cleanup Job (Backend)

```csharp
// DataRetentionService.cs (IHostedService)
public class DataRetentionService : IHostedService
{
    private Timer _timer;
    
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _timer = new Timer(CleanupOldData, null, TimeSpan.Zero, TimeSpan.FromHours(24));
        return Task.CompletedTask;
    }
    
    private void CleanupOldData(object state)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OrchestratorDbContext>();
        
        var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);
        
        // Delete old logs
        var oldLogs = db.DeviceLogs.Where(l => l.Timestamp < thirtyDaysAgo);
        db.DeviceLogs.RemoveRange(oldLogs);
        
        // Delete old metrics
        var oldMetrics = db.DeviceMetrics.Where(m => m.Timestamp < thirtyDaysAgo);
        db.DeviceMetrics.RemoveRange(oldMetrics);
        
        db.SaveChanges();
        _logger.LogInformation($"Cleaned up {oldLogs.Count()} logs and {oldMetrics.Count()} metrics");
    }
}
```

### Android Cleanup

```kotlin
// Cleans up old local logs/metrics (keep only 7 days)
class LocalCleanupService @Inject constructor(
    private val logDao: LogDao,
    private val metricDao: MetricDao
) {
    suspend fun cleanup() {
        val sevenDaysAgo = System.currentTimeMillis() - (7 * 24 * 60 * 60 * 1000)
        
        logDao.deleteOlderThan(sevenDaysAgo)
        metricDao.deleteOlderThan(sevenDaysAgo)
    }
}
```

---

## Disaster Recovery

### Restore from Backup
```bash
# Stop backend
net stop OrchestratorBackend

# Restore database
copy D:\backups\orchestrator_20260428_020000.db D:\orchestrator.db

# Start backend
net start OrchestratorBackend

# Verify
curl http://localhost:5000/api/devices
```

### Reset All Data
```bash
# Delete database (WARNING: PERMANENT)
rm D:\orchestrator.db

# Stop backend and MQTT
net stop OrchestratorBackend
net stop mosquitto

# Restart (will recreate empty DB)
net start mosquitto
net start OrchestratorBackend

# Verify
curl http://localhost:5000/api/devices  # Returns []
```

---

## Scaling Beyond 20 Devices

### Database Migration (SQLite → PostgreSQL)

Update `Program.cs`:
```csharp
if (builder.Environment.IsProduction())
{
    var pgConnection = builder.Configuration.GetConnectionString("PostgreSQL");
    builder.Services.AddDbContext<OrchestratorDbContext>(options =>
        options.UseNpgsql(pgConnection));
}
```

**appsettings.json:**
```json
{
  "ConnectionStrings": {
    "PostgreSQL": "Host=db.internal;Database=orchestrator;Username=user;Password=pwd"
  }
}
```

### MQTT Broker Clustering

Use HiveMQ cluster or EMQX for multi-node MQTT:
```
Node 1: 192.168.1.100:1883
Node 2: 192.168.1.101:1883
Node 3: 192.168.1.102:1883
Load Balancer: 192.168.1.200:1883
```

### Backend Horizontal Scaling

- Stateless API design ✓
- Shared PostgreSQL database ✓
- Redis cache for session state ✓
- Load balancer (nginx) ✓

Architecture:
```
[nginx Load Balancer :5000]
    ↓       ↓       ↓
[API-1] [API-2] [API-3]
    ↓       ↓       ↓
[PostgreSQL] (shared)
```
