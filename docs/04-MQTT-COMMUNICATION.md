# MQTT Communication Protocol

## Broker Setup

**Mosquitto** (external service on localhost:1883)

```bash
# Windows: choco install mosquitto
# Linux: sudo apt-get install mosquitto mosquitto-clients
# macOS: brew install mosquitto

# Start broker (listen on :1883)
mosquitto -c /etc/mosquitto/mosquitto.conf
```

**Configuration** (`mosquitto.conf`):
```
listener 1883
protocol mqtt

# No authentication (LAN only)
allow_anonymous true
```

---

## Connection Parameters

### Backend (MQTTnet Client)
- **Host:** `localhost` (same PC)
- **Port:** `1883`
- **ClientId:** `orchestrator-backend`
- **CleanSession:** `false` (persist subscriptions + undelivered messages)
- **QoS (publish):** `1` (at least once delivery)
- **KeepAlive:** `60` seconds

### Android (HiveMQ Client)
- **Host:** `192.168.x.x` (Windows PC IP on LAN)
- **Port:** `1883`
- **ClientId:** `device-{deviceSerial}` (e.g., `device-R38M717AB0C`)
- **CleanSession:** `false` (critical for offline queuing)
- **QoS (publish):** `1`
- **AutoReconnect:** `true` (reconnects automatically on disconnect)
- **MaxReconnectDelay:** `30` seconds

---

## Topic Hierarchy

```
orchestrator/
├── devices/
│   ├── {deviceId}/
│   │   ├── commands       ← Backend publishes, device subscribes
│   │   ├── status        ← Device publishes heartbeat/status
│   │   ├── logs          ← Device publishes execution logs
│   │   ├── metrics       ← Device publishes CPU/RAM/battery
│   │   └── ack           ← Device publishes command ACK
│   │
│   └── {deviceId}/...
│
└── broadcast/
    └── commands          ← Backend publishes, ALL devices subscribe
```

---

## Message Payloads

### 1. Assign Script (Backend → Device)

**Topic:** `orchestrator/devices/{deviceId}/commands`  
**QoS:** `1`  
**Retained:** `false`

**Payload (JSON):**
```json
{
  "commandId": "cmd-a1b2c3d4-e5f6-47a8-9b0c-1d2e3f4a5b6c",
  "type": "ASSIGN_SCRIPT",
  "timestamp": "2026-04-28T14:32:00Z",
  "payload": {
    "scriptId": "660f8400-e29b-41d4-a716-446655440001",
    "script": {
      "scriptId": "660f8400-e29b-41d4-a716-446655440001",
      "name": "Daily Login Test",
      "version": 2,
      "loopCount": -1,
      "steps": [
        {
          "stepId": "s1",
          "type": "LAUNCH_APP",
          "params": { "packageName": "com.example.app" },
          "timeoutMs": 5000,
          "onFailure": "RETRY",
          "retryCount": 3
        },
        {
          "stepId": "s2",
          "type": "WAIT",
          "params": { "durationMs": 1500 }
        },
        {
          "stepId": "s3",
          "type": "CLICK",
          "params": { "selector": "resource-id:com.example:id/login_button" }
        }
      ]
    }
  },
  "requiresAck": true
}
```

**Device receives:**
1. Parses JSON → CommandEnvelope
2. Validates scriptId + version
3. Saves script to Room DB (ScriptEntity)
4. Publishes ACK to `orchestrator/devices/{deviceId}/ack`
5. Starts ScriptExecutionEngine

---

### 2. Broadcast Script (Backend → All Devices)

**Topic:** `orchestrator/broadcast/commands`  
**QoS:** `1`  
**Retained:** `false`

**Payload (JSON):**
```json
{
  "commandId": "cmd-x1y2z3w4-a5b6-47c8-9d0e-1f2g3h4i5j6k",
  "type": "ASSIGN_SCRIPT",
  "timestamp": "2026-04-28T14:35:00Z",
  "payload": {
    "scriptId": "660f8400-e29b-41d4-a716-446655440001",
    "script": { ...full script object... }
  },
  "requiresAck": true
}
```

**All subscribed devices receive → same logic as individual assign**

---

### 3. Pause Execution (Backend → Device)

**Topic:** `orchestrator/devices/{deviceId}/commands`  
**Payload:**
```json
{
  "commandId": "cmd-p1p2p3p4-e5f6-47a8-9b0c-1d2e3f4a5b6c",
  "type": "PAUSE_EXECUTION",
  "timestamp": "2026-04-28T14:36:00Z",
  "payload": {},
  "requiresAck": true
}
```

**Device action:** Sets state = PAUSED, stops step execution (retains position).

---

### 4. Resume Execution (Backend → Device)

**Topic:** `orchestrator/devices/{deviceId}/commands`  
**Payload:**
```json
{
  "commandId": "cmd-r1r2r3r4-e5f6-47a8-9b0c-1d2e3f4a5b6c",
  "type": "RESUME_EXECUTION",
  "timestamp": "2026-04-28T14:37:00Z",
  "payload": {},
  "requiresAck": true
}
```

**Device action:** Sets state = EXECUTING, resumes from paused position.

---

### 5. Abort Execution (Backend → Device)

**Topic:** `orchestrator/devices/{deviceId}/commands`  
**Payload:**
```json
{
  "commandId": "cmd-ab-cd-ef-gh",
  "type": "ABORT_EXECUTION",
  "timestamp": "2026-04-28T14:38:00Z",
  "payload": {},
  "requiresAck": true
}
```

**Device action:** Stops script immediately, sets state = IDLE, discards all progress.

---

### 6. Device Status (Device → Backend)

**Topic:** `orchestrator/devices/{deviceId}/status`  
**QoS:** `1`  
**Published:** Every 30 seconds (heartbeat) OR on state change

**Payload:**
```json
{
  "deviceId": "550e8400-e29b-41d4-a716-446655440000",
  "deviceSerial": "R38M717AB0C",
  "state": "EXECUTING",
  "currentScriptId": "660f8400-e29b-41d4-a716-446655440001",
  "currentScriptVersion": 2,
  "currentStepId": "s3",
  "timestamp": "2026-04-28T14:32:30Z",
  "uptimeMs": 3600000,
  "batteryPercent": 78
}
```

**Backend receives:**
1. Parses → updates Device.LastSeen, Device.State, Device.CurrentScriptId
2. No response needed

---

### 7. Command Acknowledgment (Device → Backend)

**Topic:** `orchestrator/devices/{deviceId}/ack`  
**QoS:** `1`  
**Published immediately:** After device processes command

**Payload:**
```json
{
  "commandId": "cmd-a1b2c3d4-e5f6-47a8-9b0c-1d2e3f4a5b6c",
  "status": "RECEIVED",
  "timestamp": "2026-04-28T14:32:05Z",
  "notes": "Script stored to Room DB, execution starting"
}
```

**Backend receives:**
1. Logs ACK for debugging
2. Can trigger UI notification: "Device received command"

**ACK Statuses:**
- `RECEIVED`: Command parsed + accepted
- `STORED`: Script saved to Room DB
- `EXECUTING`: Script started
- `COMPLETED`: Script finished (final ACK)
- `ERROR`: Command processing failed

---

### 8. Execution Log (Device → Backend)

**Topic:** `orchestrator/devices/{deviceId}/logs`  
**QoS:** `1`  
**Published:** During script execution (per step) OR immediately on error

**Payload (single log entry):**
```json
{
  "logId": "local-uuid-123",
  "scriptId": "660f8400-e29b-41d4-a716-446655440001",
  "stepId": "s3",
  "level": "INFO",
  "message": "Clicked resource-id:com.example:id/login_button",
  "timestamp": "2026-04-28T14:32:15Z"
}
```

**Backend receives:**
1. Creates DeviceLog in SQLite (Synced=true, immediate)
2. No response needed

**Optimization:** Device batches 10 logs → 1 MQTT publish to reduce broker load.

---

### 9. Device Metrics (Device → Backend)

**Topic:** `orchestrator/devices/{deviceId}/metrics`  
**QoS:** `1`  
**Published:** Every 30 seconds

**Payload:**
```json
{
  "timestamp": "2026-04-28T14:32:30Z",
  "cpuPercent": 42.5,
  "ramUsedMb": 512,
  "batteryPercent": 78,
  "networkRxBytes": 524288000,
  "networkTxBytes": 131072000
}
```

**Backend receives:**
1. Creates DeviceMetric in SQLite (Synced=true, immediate)
2. No response needed

---

## Offline Resilience Flow

### Device Goes Offline

```
1. MqttManager.onConnectionLost()
   ↓
2. AgentForegroundService.setMode(OFFLINE)
   ↓
3. ScriptExecutionEngine continues
   - Read script from Room DB (not memory)
   - Execute steps normally
   - Write logs to Room DB: logEntity.synced = false
   - Write metrics to Room DB: metricEntity.synced = false
   ↓
4. No MQTT publishes possible (offline)
```

### Device Reconnects

```
1. NetworkChangeReceiver fires (or auto-reconnect)
   ↓
2. MqttManager.connect() succeeds
   ↓
3. MqttManager.onConnectionSuccess()
   ↓
4. MqttTopicRouter.subscribe(all topics)
   ↓
5. Broker delivers queued commands
   (because cleanSession=false, QoS=1)
   ↓
6. Device processes pending commands
   ↓
7. LogUploadService.flushPendingLogs()
   - Query Room DB: WHERE synced=false
   - Batch POST /api/logs/batch
   - On success: update Room DB: synced=true
   ↓
8. MetricsCollectionService.flush()
   - Same pattern for metrics
```

**Critical:** Broker holds commands for up to 1 hour (configurable) while device offline.

---

## Message Size & Bandwidth

### Per-Device Bandwidth

**Status publish:** ~150 bytes / 30s = **0.3 KB/min**

**Log publish (batched):** ~50 bytes × 10 logs / publish = **500 bytes / batch**  
- Typical: 5 publishes/min during execution = **2.5 KB/min**

**Metric publish:** ~150 bytes / 30s = **0.3 KB/min**

**Total per device (active):** ~3 KB/min = **180 KB/hour**

**20 devices × 3 KB/min = 60 KB/min** (sustainable on home Wi-Fi)

---

## Topic Subscriptions

### Backend
```csharp
// MqttClientService.cs
await client.SubscribeAsync(
    "orchestrator/devices/+/status",
    "orchestrator/devices/+/logs",
    "orchestrator/devices/+/metrics",
    "orchestrator/devices/+/ack"
);
```

### Android
```kotlin
// MqttManager.kt
client.subscribeWith()
    .topicFilter("orchestrator/devices/{deviceId}/commands")
    .qos(MqttQos.AT_LEAST_ONCE)
    .noLocal(false)
    .retain(false)
    .send()
    
client.subscribeWith()
    .topicFilter("orchestrator/broadcast/commands")
    .qos(MqttQos.AT_LEAST_ONCE)
    .send()
```

---

## Debugging

### Mosquitto CLI Tools

**Monitor all messages:**
```bash
mosquitto_sub -h localhost -t "orchestrator/#" -v
```

**Publish test command:**
```bash
mosquitto_pub -h localhost -t "orchestrator/devices/test-device/commands" \
  -m '{"commandId":"test-1","type":"ASSIGN_SCRIPT",...}'
```

### Log Topics
- Backend logs: Console (Serilog)
- Device logs: Logcat (`adb logcat com.orchestrator.agent:I`)
- MQTT: `mosquitto.log` (enable with `log_dest file /var/log/mosquitto/mosquitto.log`)

---

## Production Hardening

### Security
1. **Enable authentication:** Set username/password in `mosquitto.conf`
2. **TLS/SSL:** Use port 8883 with certificate
3. **Topic ACLs:** Restrict device publish to own `devices/{id}/*`, block cross-device access

### Reliability
1. **Persistent storage:** Enable `persistence true` in `mosquitto.conf`
2. **Bridge to cloud:** Forward to AWS IoT Core / Azure IoT Hub for redundancy
3. **Max connections:** Set `max_connections` limit (e.g., 50 for 20 devices + backend)

### Monitoring
1. **Broker stats:** `mosquitto -v` or plugin for Prometheus metrics
2. **Connection tracking:** Monitor `$SYS/broker/clients/connected`
3. **Message rates:** Alert on pub rate drops (device offline longer than expected)
