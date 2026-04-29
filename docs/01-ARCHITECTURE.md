# Orchestrator System Architecture

## Overview

Android device orchestration system for 20x Samsung S8 Active phones (Android 9). Local Windows PC backend controls distributed script execution with autonomous offline resilience.

## System Design

```
┌──────────────────────────────────────────────────┐
│         Windows PC (Backend, .NET 10)             │
│  ┌─────────────┐  ┌──────────────┐               │
│  │  ASP.NET    │  │Orchestration │               │
│  │  REST API   │  │  Service     │               │
│  │  :5000      │  │              │               │
│  └──────┬──────┘  └──────┬───────┘               │
│         │                │                       │
│  ┌──────▼────────────────▼──────────┐            │
│  │  SQLite Database (orchestrator.db)            │
│  │  - Devices, Scripts, Logs, Metrics            │
│  └────────────────────────────────────┘          │
│         │                                        │
│  ┌──────▼──────────────────────────┐             │
│  │  MQTT Broker (Mosquitto :1883)   │            │
│  │  - QoS 1, cleanSession=false     │            │
│  └────────────────────────────────────┘          │
└─────────────┬──────────────────────────────────┘
              │ Wi-Fi LAN (192.168.x.x)
   ┌──────────┼──────────┐
   │          │          │
┌──▼──┐   ┌──▼──┐   ┌──▼──┐
│ S8  │   │ S8  │   │ S8  │  (20 devices total)
│Active   │Active   │Active
│  +1 │   │  +2 │   │ +20 │
│Kotlin  │Kotlin  │Kotlin
│MQTT    │MQTT    │MQTT
│Room DB │Room DB │Room DB
└─────┘   └─────┘   └─────┘
```

## Communication Protocol: MQTT

**Why MQTT over WebSocket/gRPC/ADB?**
- QoS 1/2 guarantees delivery + automatic retry
- `cleanSession=false` queues commands while device offline
- Pub/sub native broadcasting to 20 devices simultaneously
- Designed for IoT with many devices
- Built-in offline resilience

**MQTT Topics:**
```
orchestrator/devices/{deviceId}/commands    ← Backend → Device
orchestrator/devices/{deviceId}/status      ← Device → Backend
orchestrator/devices/{deviceId}/logs        ← Device telemetry
orchestrator/devices/{deviceId}/metrics     ← Device metrics
orchestrator/devices/{deviceId}/ack         ← Command acknowledgment
orchestrator/broadcast/commands             ← Broadcast to all
```

## Offline Resilience Model

### Layer 1: MQTT Broker Persistence
- Device connects with `cleanSession=false`
- Broker queues commands while offline
- Auto-delivery on reconnect

### Layer 2: Device Autonomous Operation
- Script stored in Room DB (not memory)
- Execution loop continues without network
- Logs/metrics buffered to Room DB
- Auto-reconnect via NetworkChangeReceiver
- BootReceiver restarts service after reboot

**State Machine:**
```
IDLE → LOADING_SCRIPT → EXECUTING
                           ↓ (no network)
                    OFFLINE_EXECUTING
                           ↓ (network back)
                    SYNCING_DATA → EXECUTING
```

## Technology Stack

### Backend
| Layer | Tech | Purpose |
|-------|------|---------|
| **Runtime** | .NET 10 | Cross-platform |
| **API** | ASP.NET Core | REST endpoints |
| **Database** | EF Core + SQLite | ORM + data persistence |
| **Messaging** | MQTTnet | MQTT client/broker |
| **Logging** | Serilog | Structured logging |
| **Testing** | xUnit + Moq | Unit/integration tests |

### Android
| Layer | Tech | Purpose |
|-------|------|---------|
| **Language** | Kotlin | Type-safe Android dev |
| **Database** | Room | Local persistence |
| **MQTT** | HiveMQ Client | MQTT communication |
| **DI** | Hilt | Dependency injection |
| **UI Automation** | AccessibilityService | Click/input in other apps |
| **Async** | Coroutines | Non-blocking execution |

## Project Structure

**Backend:**
```
src/
├── Orchestrator.Core          ← Models, interfaces, enums
├── Orchestrator.Infrastructure ← EF Core, MQTT, repositories
├── Orchestrator.Application    ← Business logic, DTOs
└── Orchestrator.Api           ← Controllers, Program.cs
```

**Android:**
```
app/src/main/kotlin/com/orchestrator/agent/
├── core/di/                   ← Hilt modules
├── data/
│   ├── local/                 ← Room DB
│   └── remote/                ← MQTT, REST
├── execution/                 ← Script engine
├── services/                  ← Foreground, accessibility
└── receivers/                 ← Boot, network changes
```

## Deployment Model

**Development:**
- Backend: `dotnet run` (includes embedded MQTT broker option)
- Devices: Test via ADB or physical S8 phones
- Database: SQLite (auto-created)

**Production:**
- Backend: Windows Service or IIS
- Mosquitto: Standalone service on same PC
- Database: SQLite or upgrade to PostgreSQL as scale demands
- Devices: APK deployed via ADB to 20 phones

## Key Design Decisions

1. **MQTT over proprietary protocol** → Proven IoT standard, offline queuing built-in
2. **SQLite not PostgreSQL** → Local-only system, zero ops overhead at 20-device scale
3. **Room DB on Android** → Scripts persist locally, execution continues offline
4. **AccessibilityService for automation** → No root needed, one-time permission
5. **Foreground service + WakeLock** → Android doesn't kill orchestration service
6. **InMemory DB for tests** → Test isolation, no DB fixtures needed

## Data Flow Example: Script Execution

```
1. Backend creates script
   POST /api/scripts → saved to SQLite

2. Backend assigns to device
   POST /api/orchestration/assign 
   → OrchestrationService.AssignScriptAsync()
   → MqttPublisher.PublishAsync("orchestrator/devices/{id}/commands")

3. Device receives command via MQTT
   MqttManager.onApplicationMessageReceived()
   → MqttMessageHandler.route()
   → ScriptExecutionEngine.startScript()

4. Engine executes steps locally
   for each step in script:
     StepExecutor.execute()
     → LogRepository.writeLog()  [to Room DB + MQTT]
     → MetricsCollectionService.collect()

5. Device publishes telemetry
   LogUploadService: batch POST /api/logs/batch

6. Backend ingests and stores
   LogsController.UploadBatch()
   → LogRepository.CreateAsync()
   → SQLite persists
```

## Security Considerations

- **MQTT Authentication**: Disabled for LAN (internal network only)
- **Script Validation**: Backend validates JSON schema before pushing
- **Data Encryption**: Consider TLS for MQTT in production
- **Device Registration**: Manual APK deployment (no remote install)
- **Log Sensitivity**: No PII in logs, sanitize before storage

## Scalability Notes

**Current Design (20 devices):**
- SQLite handles ~60k rows/day (metrics: 20 devices × 30s interval × 288/day)
- MQTT broker embedded or single Mosquitto service
- Single Windows PC backend

**Future (100+ devices):**
- Migrate to PostgreSQL
- Separate MQTT broker cluster
- Backend horizontal scaling (stateless API)
- Log archival to object storage
- Metrics pipeline (Prometheus/Grafana)
