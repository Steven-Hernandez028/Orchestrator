# REST API Reference

## Base URL
```
http://localhost:5000/api
```

---

## Devices

### Register Device
**POST** `/devices/register`

Android phone self-registers on first launch or missed heartbeat.

**Request:**
```json
{
  "deviceSerial": "R38M717AB0C",
  "friendlyName": "Lab Phone 1",
  "androidVersion": "9"
}
```

**Response:** `201 Created`
```json
{
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "deviceSerial": "R38M717AB0C",
  "friendlyName": "Lab Phone 1",
  "androidVersion": "9",
  "state": "IDLE",
  "lastSeen": "2026-04-28T14:32:00Z",
  "currentScriptId": null
}
```

**Error:** `400 Bad Request` if serial already exists
```json
{
  "error": "Device with serial R38M717AB0C already registered"
}
```

---

### Get All Devices
**GET** `/devices`

List all registered phones.

**Response:** `200 OK`
```json
[
  {
    "id": "550e8400-e29b-41d4-a716-446655440000",
    "deviceSerial": "R38M717AB0C",
    "friendlyName": "Lab Phone 1",
    "androidVersion": "9",
    "state": "EXECUTING",
    "lastSeen": "2026-04-28T14:32:00Z",
    "currentScriptId": "660f8400-e29b-41d4-a716-446655440001"
  },
  {
    "id": "660f8400-e29b-41d4-a716-446655440002",
    "deviceSerial": "R38M717AB0D",
    "friendlyName": "Lab Phone 2",
    "androidVersion": "9",
    "state": "IDLE",
    "lastSeen": "2026-04-28T14:30:00Z",
    "currentScriptId": null
  }
]
```

---

### Get Device by ID
**GET** `/devices/{id}`

**Response:** `200 OK`
```json
{
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "deviceSerial": "R38M717AB0C",
  "friendlyName": "Lab Phone 1",
  "androidVersion": "9",
  "state": "EXECUTING",
  "lastSeen": "2026-04-28T14:32:00Z",
  "currentScriptId": "660f8400-e29b-41d4-a716-446655440001"
}
```

**Error:** `404 Not Found` if device doesn't exist

---

### Update Device Name
**PUT** `/devices/{id}/name`

**Request:**
```json
{
  "friendlyName": "Lab Phone 1 (Backup)"
}
```

**Response:** `200 OK`
```json
{
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "deviceSerial": "R38M717AB0C",
  "friendlyName": "Lab Phone 1 (Backup)",
  "androidVersion": "9",
  "state": "EXECUTING",
  "lastSeen": "2026-04-28T14:32:00Z",
  "currentScriptId": "660f8400-e29b-41d4-a716-446655440001"
}
```

---

### Delete Device
**DELETE** `/devices/{id}`

Unregister phone (doesn't stop execution, just removes from DB).

**Response:** `204 No Content`

---

## Scripts

### Get All Scripts
**GET** `/scripts`

List all script versions (latest + archived).

**Response:** `200 OK`
```json
[
  {
    "id": "660f8400-e29b-41d4-a716-446655440001",
    "name": "Daily Login Test",
    "version": 3,
    "jsonDefinition": "{...}",
    "createdAt": "2026-04-20T10:00:00Z",
    "updatedAt": "2026-04-28T12:00:00Z"
  }
]
```

---

### Get Script by ID
**GET** `/scripts/{id}`

Retrieve specific script (latest version).

**Response:** `200 OK`
```json
{
  "id": "660f8400-e29b-41d4-a716-446655440001",
  "name": "Daily Login Test",
  "version": 3,
  "jsonDefinition": "{\"scriptId\":\"660f8400-e29b-41d4-a716-446655440001\",\"name\":\"Daily Login Test\",\"version\":3,\"loopCount\":-1,\"steps\":[{\"stepId\":\"s1\",\"type\":\"LAUNCH_APP\",\"params\":{\"packageName\":\"com.example.app\"},\"timeoutMs\":5000,\"onFailure\":\"RETRY\",\"retryCount\":3}]}",
  "createdAt": "2026-04-20T10:00:00Z",
  "updatedAt": "2026-04-28T12:00:00Z"
}
```

---

### Create Script
**POST** `/scripts`

Write new script (version=1 auto-assigned).

**Request:**
```json
{
  "name": "Daily Login Test",
  "jsonDefinition": "{\"scriptId\":null,\"name\":\"Daily Login Test\",\"version\":1,\"loopCount\":-1,\"steps\":[{\"stepId\":\"s1\",\"type\":\"LAUNCH_APP\",\"params\":{\"packageName\":\"com.example.app\"},\"timeoutMs\":5000,\"onFailure\":\"RETRY\",\"retryCount\":3},{\"stepId\":\"s2\",\"type\":\"WAIT\",\"params\":{\"durationMs\":1500}}]}"
}
```

**Response:** `201 Created`
```json
{
  "id": "660f8400-e29b-41d4-a716-446655440001",
  "name": "Daily Login Test",
  "version": 1,
  "jsonDefinition": "{...}",
  "createdAt": "2026-04-28T14:32:00Z",
  "updatedAt": "2026-04-28T14:32:00Z"
}
```

---

### Update Script
**PUT** `/scripts/{id}`

Modify script → increments version (v1→v2→v3).

**Request:**
```json
{
  "name": "Daily Login Test (Updated)",
  "jsonDefinition": "{\"steps\":[...new steps...]}"
}
```

**Response:** `200 OK`
```json
{
  "id": "660f8400-e29b-41d4-a716-446655440001",
  "name": "Daily Login Test (Updated)",
  "version": 2,
  "jsonDefinition": "{...}",
  "createdAt": "2026-04-20T10:00:00Z",
  "updatedAt": "2026-04-28T14:35:00Z"
}
```

---

### Delete Script
**DELETE** `/scripts/{id}`

Soft-delete script (marks deleted, keeps for audit).

**Response:** `204 No Content`

---

## Orchestration

### Assign Script to Device
**POST** `/orchestration/assign`

Push script to single device. Publishes MQTT command → device receives, starts execution.

**Request:**
```json
{
  "deviceId": "550e8400-e29b-41d4-a716-446655440000",
  "scriptId": "660f8400-e29b-41d4-a716-446655440001"
}
```

**Response:** `202 Accepted` (command queued, device execution async)
```json
{
  "commandId": "cmd-a1b2c3d4",
  "status": "QUEUED",
  "message": "Script assignment published to device"
}
```

**Backend action:**
1. Validates device + script exist
2. Creates CommandEnvelope: `{ "commandId": "...", "type": "ASSIGN_SCRIPT", "payload": { "scriptId": "..." }, "requiresAck": true }`
3. Publishes to `orchestrator/devices/{deviceId}/commands` (QoS=1)
4. Device ACKs via `orchestrator/devices/{deviceId}/ack`

---

### Broadcast Script to All Devices
**POST** `/orchestration/broadcast`

Push script to all devices simultaneously (pub/sub).

**Request:**
```json
{
  "scriptId": "660f8400-e29b-41d4-a716-446655440001"
}
```

**Response:** `202 Accepted`
```json
{
  "commandId": "cmd-x1y2z3w4",
  "status": "QUEUED",
  "message": "Broadcast published to all devices",
  "affectedDeviceCount": 18
}
```

**Backend action:**
1. Publishes to `orchestrator/broadcast/commands` (QoS=1)
2. All connected devices receive via subscription

---

### Pause Execution
**POST** `/orchestration/pause`

Pause current script on device (device retains state).

**Request:**
```json
{
  "deviceId": "550e8400-e29b-41d4-a716-446655440000"
}
```

**Response:** `202 Accepted`
```json
{
  "commandId": "cmd-p1p2p3p4",
  "status": "QUEUED",
  "message": "Pause command sent to device"
}
```

---

### Resume Execution
**POST** `/orchestration/resume`

Resume paused script on device.

**Request:**
```json
{
  "deviceId": "550e8400-e29b-41d4-a716-446655440000"
}
```

**Response:** `202 Accepted`

---

### Abort Execution
**POST** `/orchestration/abort`

Stop script immediately on device.

**Request:**
```json
{
  "deviceId": "550e8400-e29b-41d4-a716-446655440000"
}
```

**Response:** `202 Accepted`

---

## Logs

### Get Device Logs
**GET** `/logs`

Retrieve logs from execution. Supports filtering + pagination.

**Query Parameters:**
- `deviceId` (required): Filter by device
- `from` (optional): ISO8601 start time (e.g., `2026-04-28T00:00:00Z`)
- `to` (optional): ISO8601 end time
- `level` (optional): "INFO", "WARN", "ERROR"
- `page` (optional, default=1): Page number (20 per page)

**Request:**
```
GET /logs?deviceId=550e8400-e29b-41d4-a716-446655440000&from=2026-04-28T00:00:00Z&level=ERROR&page=1
```

**Response:** `200 OK`
```json
[
  {
    "id": "770g8400-e29b-41d4-a716-446655440005",
    "deviceId": "550e8400-e29b-41d4-a716-446655440000",
    "scriptId": "660f8400-e29b-41d4-a716-446655440001",
    "stepId": "s3",
    "level": "ERROR",
    "message": "Click timeout after 3 retries",
    "timestamp": "2026-04-28T14:32:15Z",
    "synced": true
  }
]
```

---

### Batch Upload Logs
**POST** `/logs/batch`

Android device uploads buffered logs (called on reconnect or periodically).

**Request:**
```json
{
  "deviceId": "550e8400-e29b-41d4-a716-446655440000",
  "logs": [
    {
      "logId": "local-123",
      "scriptId": "660f8400-e29b-41d4-a716-446655440001",
      "stepId": "s1",
      "level": "INFO",
      "message": "Launched app",
      "timestamp": "2026-04-28T14:30:00Z"
    },
    {
      "logId": "local-124",
      "scriptId": "660f8400-e29b-41d4-a716-446655440001",
      "stepId": "s2",
      "level": "INFO",
      "message": "Waited 1500ms",
      "timestamp": "2026-04-28T14:30:02Z"
    }
  ]
}
```

**Response:** `202 Accepted`
```json
{
  "message": "Logs accepted for processing",
  "count": 2,
  "logIds": ["local-123", "local-124"]
}
```

**Backend action:**
1. Creates DeviceLog entries (Synced=true)
2. Returns list of accepted logIds
3. Device marks these as synced in Room DB

---

## Metrics

### Get Latest Device Metrics
**GET** `/metrics/{deviceId}/latest`

Most recent CPU/RAM/battery/network snapshot.

**Response:** `200 OK`
```json
{
  "id": "880h8400-e29b-41d4-a716-446655440006",
  "deviceId": "550e8400-e29b-41d4-a716-446655440000",
  "timestamp": "2026-04-28T14:32:30Z",
  "cpuPercent": 42.5,
  "ramUsedMb": 512,
  "batteryPercent": 78,
  "networkRxBytes": 524288000,
  "networkTxBytes": 131072000,
  "synced": true
}
```

---

### Get Device Metrics (Time Range)
**GET** `/metrics`

Historical metrics with time filtering.

**Query Parameters:**
- `deviceId` (required): Filter by device
- `from` (optional): ISO8601 start time
- `to` (optional): ISO8601 end time
- `page` (optional, default=1): Pagination (20 per page)

**Request:**
```
GET /metrics?deviceId=550e8400-e29b-41d4-a716-446655440000&from=2026-04-28T00:00:00Z&to=2026-04-28T23:59:59Z
```

**Response:** `200 OK`
```json
[
  {
    "id": "880h8400-e29b-41d4-a716-446655440006",
    "deviceId": "550e8400-e29b-41d4-a716-446655440000",
    "timestamp": "2026-04-28T14:30:00Z",
    "cpuPercent": 35.2,
    "ramUsedMb": 480,
    "batteryPercent": 82,
    "networkRxBytes": 520192000,
    "networkTxBytes": 130560000,
    "synced": true
  },
  {
    "timestamp": "2026-04-28T14:30:30Z",
    "cpuPercent": 42.5,
    "ramUsedMb": 512,
    "batteryPercent": 78,
    "networkRxBytes": 524288000,
    "networkTxBytes": 131072000,
    "synced": true
  }
]
```

---

## Error Handling

All errors return JSON with `error` and optional `details`:

```json
{
  "error": "Device not found",
  "details": "Device ID 550e8400-e29b-41d4-a716-446655440099 does not exist"
}
```

### Status Codes
| Code | Meaning |
|------|---------|
| `200` | Success (GET, PUT) |
| `201` | Created (POST for creation) |
| `202` | Accepted (async commands: assign, broadcast, pause/resume/abort) |
| `204` | No Content (DELETE) |
| `400` | Bad Request (validation error) |
| `404` | Not Found (resource doesn't exist) |
| `409` | Conflict (e.g., duplicate device serial) |
| `500` | Internal Server Error (unexpected) |

---

## Request/Response DTOs

### CreateScriptRequest
```csharp
public class CreateScriptRequest
{
    public string Name { get; set; }
    public string JsonDefinition { get; set; }
}
```

### UpdateScriptRequest
```csharp
public class UpdateScriptRequest
{
    public string Name { get; set; }
    public string JsonDefinition { get; set; }
}
```

### AssignScriptRequest
```csharp
public class AssignScriptRequest
{
    public string DeviceId { get; set; }
    public string ScriptId { get; set; }
}
```

### BroadcastScriptRequest
```csharp
public class BroadcastScriptRequest
{
    public string ScriptId { get; set; }
}
```

### PauseExecutionRequest
```csharp
public class PauseExecutionRequest
{
    public string DeviceId { get; set; }
}
```

### BatchLogsRequest
```csharp
public class BatchLogsRequest
{
    public string DeviceId { get; set; }
    public List<LogEntryDto> Logs { get; set; }
}

public class LogEntryDto
{
    public string LogId { get; set; }
    public string ScriptId { get; set; }
    public string StepId { get; set; }
    public string Level { get; set; }
    public string Message { get; set; }
    public DateTime Timestamp { get; set; }
}
```
