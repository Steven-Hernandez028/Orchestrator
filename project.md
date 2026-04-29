 Orchestrator System Plan — C#/.NET + Kotlin Android                                                                    
 Context

 Sistema de orquestación local para 20x Samsung S8 Active (Android 9) conectados vía Wi-Fi LAN a una PC Windows. El
 backend controla, distribuye scripts de automatización y recolecta telemetría. Requisito crítico: si el backend cae,
 cada teléfono continúa ejecutando su script autónomamente.

 ---
 Architecture Overview

 ┌──────────────────────────────────────────────────────┐
 │                Windows PC (Backend)                   │
 │  ┌─────────────┐  ┌──────────────┐  ┌─────────────┐ │
 │  │  ASP.NET    │  │Orchestration │  │  MQTTnet    │ │
 │  │  REST API   │  │  Service     │  │  Broker     │ │
 │  │  :5000      │  │              │  │  :1883      │ │
 │  └──────┬──────┘  └──────┬───────┘  └──────┬──────┘ │
 │         └────────────────┴──────────────────┘        │
 │                      SQLite DB                       │
 └────────────────────────┬─────────────────────────────┘
                          │ Wi-Fi LAN
           ┌──────────────┼──────────────┐
    ┌──────▼──────┐ ┌─────▼──────┐ ┌────▼───────┐
    │  S8 Active  │ │ S8 Active  │ │ S8 Active  │
    │  MQTT+Room  │ │ MQTT+Room  │ │ MQTT+Room  │
    │  Autonomous │ │ Autonomous │ │ Autonomous │
    └─────────────┘ └────────────┘ └────────────┘

 ---
 Protocol Decision: MQTT (primary) + REST (bulk data)

 MQTT vence sobre WebSocket/gRPC/ADB porque:
 - QoS 1/2 garantiza entrega aunque dispositivo se desconecte temporalmente
 - cleanSession=false → Mosquitto/MQTTnet encola comandos para dispositivos offline
 - Pub/sub nativo: un publish llega a los 20 dispositivos simultáneamente
 - Diseñado para IoT con muchos dispositivos — pattern exacto del problema

 REST complementa para: registro inicial, subida de logs en batch, CRUD de scripts.

 MQTT Topic Schema:
 orchestrator/devices/{deviceId}/commands   ← backend → device
 orchestrator/devices/{deviceId}/status     ← device → backend
 orchestrator/devices/{deviceId}/logs       ← device → backend
 orchestrator/devices/{deviceId}/metrics    ← device → backend
 orchestrator/broadcast/commands            ← backend → ALL devices
 orchestrator/devices/{deviceId}/ack        ← device → backend

 ---
 Project Structure

 Backend (C#/.NET 8)

 D:\GitHub\Orchestrator\
 ├── Orchestrator.sln
 ├── src\
 │   ├── Orchestrator.Api\              ← ASP.NET Core Web API :5000
 │   │   ├── Controllers\
 │   │   │   ├── DevicesController.cs
 │   │   │   ├── ScriptsController.cs
 │   │   │   ├── LogsController.cs
 │   │   │   ├── MetricsController.cs
 │   │   │   └── OrchestrationController.cs
 │   │   └── Program.cs
 │   │
 │   ├── Orchestrator.Core\             ← Domain models + interfaces
 │   │   ├── Models\
 │   │   │   ├── Device.cs
 │   │   │   ├── Script.cs
 │   │   │   ├── CommandEnvelope.cs
 │   │   │   ├── DeviceLog.cs
 │   │   │   └── DeviceMetric.cs
 │   │   ├── Interfaces\
 │   │   │   ├── IOrchestrationService.cs
 │   │   │   ├── IMqttPublisher.cs
 │   │   │   └── IDeviceRepository.cs (+ IScriptRepository, ILogRepository, IMetricRepository)
 │   │   └── Enums\
 │   │       ├── CommandType.cs
 │   │       ├── DeviceState.cs
 │   │       └── StepType.cs
 │   │
 │   ├── Orchestrator.Infrastructure\   ← EF Core, MQTT, repositories
 │   │   ├── Data\
 │   │   │   ├── OrchestratorDbContext.cs   ← CRITICAL FILE
 │   │   │   └── Repositories\
 │   │   │       └── (DeviceRepository, ScriptRepository, LogRepository, MetricRepository)
 │   │   ├── Mqtt\
 │   │   │   ├── MqttBrokerService.cs       ← CRITICAL FILE (embedded MQTTnet server)
 │   │   │   ├── MqttClientService.cs
 │   │   │   ├── MqttTopicRouter.cs
 │   │   │   └── MqttPublisher.cs
 │   │   └── Services\
 │   │       ├── DeviceHeartbeatMonitor.cs
 │   │       └── DataRetentionService.cs    ← IHostedService, deletes logs >30 días
 │   │
 │   └── Orchestrator.Application\     ← Business logic
 │       ├── Services\
 │       │   ├── OrchestrationService.cs    ← CRITICAL FILE
 │       │   ├── ScriptService.cs
 │       │   └── TelemetryService.cs
 │       └── DTOs\
 │           └── (DeviceDto, ScriptDto, CreateScriptRequest, PushScriptRequest)
 │
 ├── tests\
 │   ├── Orchestrator.UnitTests\
 │   └── Orchestrator.IntegrationTests\
 │
 └── scripts\
     ├── setup-mosquitto.ps1
     └── deploy-apk.ps1

 Android App (Kotlin)

 D:\GitHub\Orchestrator\android\
 └── app\src\main\kotlin\com\orchestrator\agent\
     ├── OrchestratorApplication.kt
     ├── core\di\                       ← Hilt modules (AppModule, MqttModule, DatabaseModule)
     ├── data\
     │   ├── local\database\
     │   │   ├── OrchestratorDatabase.kt   ← Room DB
     │   │   ├── dao\(ScriptDao, LogDao, MetricDao)
     │   │   └── entities\(ScriptEntity, LogEntity, MetricEntity)
     │   └── remote\mqtt\
     │       ├── MqttManager.kt            ← CRITICAL FILE
     │       └── MqttMessageHandler.kt
     ├── execution\
     │   ├── ScriptExecutionEngine.kt      ← CRITICAL FILE
     │   ├── ScriptInterpreter.kt
     │   └── StepExecutors\
     │       ├── StepExecutor.kt           ← interface
     │       ├── ClickStepExecutor.kt      ← vía AccessibilityService
     │       ├── InputTextStepExecutor.kt
     │       ├── WaitStepExecutor.kt
     │       ├── LaunchAppStepExecutor.kt
     │       ├── SwipeStepExecutor.kt
     │       ├── AssertStepExecutor.kt
     │       └── HttpRequestStepExecutor.kt
     ├── services\
     │   ├── AgentForegroundService.kt     ← START_STICKY + WakeLock
     │   ├── OrchestratorAccessibilityService.kt  ← para click/input en otras apps
     │   ├── MetricsCollectionService.kt   ← cada 30s
     │   ├── LogUploadService.kt           ← batch flush on reconnect
     │   └── HeartbeatService.kt
     └── receiver\
         ├── BootReceiver.kt               ← auto-start tras reinicio
         └── NetworkChangeReceiver.kt      ← trigger MQTT reconnect

 ---
 Script Definition (JSON)

 Scripts viajan por MQTT como payload JSON inline o referenciados por ID:

 {
   "scriptId": "uuid",
   "name": "Daily Login Test",
   "version": 3,
   "loopCount": -1,
   "steps": [
     { "stepId": "s1", "type": "LAUNCH_APP",  "params": { "packageName": "com.example.app" }, "timeoutMs": 5000,
 "onFailure": "RETRY", "retryCount": 3 },
     { "stepId": "s2", "type": "WAIT",        "params": { "durationMs": 1500 } },
     { "stepId": "s3", "type": "CLICK",       "params": { "selector": "resource-id:com.example:id/login_button" } },
     { "stepId": "s4", "type": "INPUT_TEXT",  "params": { "selector": "resource-id:com.example:id/username", "text":
 "user@test.com" } },
     { "stepId": "s5", "type": "ASSERT",      "params": { "selector": "resource-id:com.example:id/welcome",
 "condition": "EXISTS" }, "onFailure": "ABORT_SCRIPT" }
   ]
 }

 Command Envelope (MQTT payload):
 { "commandId": "cmd-abc", "type": "ASSIGN_SCRIPT", "timestamp": "...", "payload": { "scriptId": "..." },
 "requiresAck": true }

 CommandTypes: ASSIGN_SCRIPT, PAUSE_EXECUTION, RESUME_EXECUTION, ABORT_EXECUTION, PUSH_SCRIPT_STORE, REQUEST_STATUS,
 UPDATE_CONFIG

 ---
 Offline Resilience Model (CRÍTICO)

 Capa 1 — MQTT QoS + Persistent Session

 - Dispositivo conecta con cleanSession=false
 - Broker encola comandos mientras el dispositivo esté offline
 - Al reconectar: recibe comandos pendientes automáticamente
 - Telemetría con QoS=1: Paho encola localmente, flush al reconectar

 Capa 2 — Autonomous Device Operation

 Backend cae
     │
     ▼
 MqttManager.onConnectionLost()
     │
     ▼
 AgentForegroundService → mode = OFFLINE
     │
     ▼
 ScriptExecutionEngine CONTINÚA (lee script de Room DB, no de memoria)
     ├─ Logs → Room DB (LogEntity, campo synced=false)
     ├─ Metrics → Room DB (MetricEntity, campo synced=false)
     └─ Loop script: if loopCount=-1 → corre indefinidamente

 Backend vuelve → NetworkChangeReceiver dispara
     │
     ▼
 MqttManager.reconnect()
     ├─ LogUploadService.flushPendingLogs() → POST /api/logs/batch
     ├─ MetricsCollectionService.flush()
     └─ Subscribe topics → recibe comandos encolados

 Reglas clave:
 - ScriptExecutionEngine siempre lee script desde ScriptDao (Room DB), nunca solo de memoria
 - AgentForegroundService con WakeLock + START_STICKY (Android no lo mata)
 - BootReceiver relanza el servicio tras reinicio del teléfono

 ---
 Database Schema (SQLite/EF Core backend)

 Devices:        Id, DeviceSerial (unique), FriendlyName, AndroidVersion, LastSeen, State, CurrentScriptId
 Scripts:        Id, Name, Version, JsonDefinition, CreatedAt, UpdatedAt
 DeviceLogs:     Id, DeviceId, ScriptId, StepId, Level, Message, Timestamp
 DeviceMetrics:  Id, DeviceId, CpuPercent, RamUsedMb, BatteryPercent, NetworkRxBytes, Timestamp
 DeviceScriptAssignments: Id, DeviceId, ScriptId, AssignedAt, CompletedAt
 Índices: DeviceLogs(DeviceId, Timestamp), DeviceMetrics(DeviceId, Timestamp), Devices(DeviceSerial)

 ---
 REST API

 GET    /api/devices                     → lista dispositivos + estado
 POST   /api/devices/register            → auto-registro del teléfono
 GET/POST/PUT/DELETE /api/scripts        → CRUD scripts
 POST   /api/orchestration/assign        → { deviceId, scriptId } → MQTT
 POST   /api/orchestration/broadcast     → { scriptId } → todos
 POST   /api/orchestration/pause|resume|abort
 GET    /api/logs?deviceId=&from=&to=&level=&page=
 POST   /api/logs/batch                  → teléfono sube logs acumulados
 GET    /api/metrics?deviceId=&from=&to=
 GET    /api/metrics/{deviceId}/latest

 ---
 Key Packages

 Backend (NuGet)

 MQTTnet (4.*)                     ← broker embebido + client
 Microsoft.EntityFrameworkCore.Sqlite (8.*)
 Serilog.AspNetCore (8.*)
 MediatR (12.*)
 FluentValidation (11.*)
 Swashbuckle.AspNetCore (6.*)       ← OpenAPI/Swagger
 xunit + Moq + FluentAssertions     ← tests

 Android (Gradle)

 com.hivemq:hivemq-mqtt-client:1.3.3          // MQTT (más moderno que Paho)
 androidx.room:room-runtime:2.6.1             // Room DB
 com.squareup.retrofit2:retrofit:2.11.0       // REST bulk upload
 com.google.dagger:hilt-android:2.51          // DI
 androidx.test.uiautomator:uiautomator:2.3.0  // UI automation
 androidx.work:work-runtime-ktx:2.9.0         // WorkManager background sync
 androidx.lifecycle:lifecycle-service:2.8.0

 Nota importante: Para click/input en OTRAS apps sin root en Android 9, se requiere AccessibilityService. El usuario
 debe otorgar permiso una vez. OrchestratorAccessibilityService maneja esto.

 ---
 Implementation Sequence

 Semana 1 — Backend foundation:
 1. Solución + proyectos + NuGet
 2. EF Core models + SQLite migrations
 3. REST endpoints (devices, scripts)
 4. MqttBrokerService.cs (embedded MQTTnet)
 5. MqttPublisher + MqttTopicRouter

 Semana 2 — Android agent:
 1. Proyecto Android + Hilt
 2. Room DB (ScriptDao, LogDao, MetricDao)
 3. AgentForegroundService + WakeLock
 4. MqttManager con cleanSession=false + auto-reconnect
 5. BootReceiver + NetworkChangeReceiver
 6. Auto-registro en primer lanzamiento → POST /api/devices/register

 Semana 3 — Script execution:
 1. ScriptExecutionEngine (coroutine loop)
 2. OrchestratorAccessibilityService
 3. StepExecutors: LAUNCH_APP, WAIT, CLICK (mínimo viable)