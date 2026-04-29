# Android Agent Architecture (Kotlin)

## Overview

Kotlin app on Samsung S8 Active (Android 9). Room DB + MQTT client + foreground service. Continues execution offline. No root required (AccessibilityService).

---

## Project Structure

```
app/src/main/kotlin/com/orchestrator/agent/
├── OrchestratorApplication.kt        ← App class (Hilt setup)
├── core/
│   └── di/
│       ├── AppModule.kt              ← Hilt: Context, preferences
│       ├── MqttModule.kt             ← Hilt: HiveMQ client, connection mgmt
│       ├── DatabaseModule.kt         ← Hilt: Room DB instance
│       └── ServiceModule.kt           ← Hilt: Service singletons
├── data/
│   ├── local/
│   │   ├── database/
│   │   │   ├── OrchestratorDatabase.kt
│   │   │   ├── dao/
│   │   │   │   ├── ScriptDao.kt
│   │   │   │   ├── LogDao.kt
│   │   │   │   └── MetricDao.kt
│   │   │   └── entities/
│   │   │       ├── ScriptEntity.kt
│   │   │       ├── LogEntity.kt
│   │   │       └── MetricEntity.kt
│   │   ├── repositories/
│   │   │   ├── ScriptRepository.kt
│   │   │   ├── LogRepository.kt
│   │   │   └── MetricRepository.kt
│   │   └── preferences/
│   │       ├── PreferencesManager.kt  ← SharedPreferences wrapper
│   │       └── DeviceInfo.kt          ← Serial, name, version
│   └── remote/
│       ├── mqtt/
│       │   ├── MqttManager.kt         ← Connection lifecycle
│       │   ├── MqttMessageHandler.kt  ← Message dispatch
│       │   └── MqttTopicSubscriber.kt
│       └── api/
│           ├── ApiService.kt          ← Retrofit interface
│           ├── LogBatchUploader.kt
│           └── DeviceRegistrar.kt
├── execution/
│   ├── ScriptExecutionEngine.kt       ← Main loop (coroutine)
│   ├── ScriptInterpreter.kt           ← JSON → steps
│   ├── ExecutionState.kt              ← Data class (status, position)
│   └── executors/
│       ├── StepExecutor.kt            ← Interface
│       ├── LaunchAppExecutor.kt       ← LAUNCH_APP
│       ├── WaitExecutor.kt            ← WAIT
│       ├── ClickExecutor.kt           ← CLICK (AccessibilityService)
│       ├── InputTextExecutor.kt       ← INPUT_TEXT
│       ├── SwipeExecutor.kt           ← SWIPE
│       ├── AssertExecutor.kt          ← ASSERT (condition checks)
│       └── HttpRequestExecutor.kt     ← HTTP_REQUEST
├── services/
│   ├── AgentForegroundService.kt      ← Lifecycle mgmt, WakeLock
│   ├── OrchestratorAccessibilityService.kt ← Click/input injection
│   ├── MetricsCollectionService.kt    ← CPU/RAM/battery (30s)
│   ├── LogUploadService.kt            ← Batch flush (on reconnect)
│   ├── HeartbeatService.kt            ← Status publish (30s)
│   └── NetworkMonitor.kt              ← Detect online/offline
├── receivers/
│   ├── BootReceiver.kt                ← Auto-start on reboot
│   └── NetworkChangeReceiver.kt       ← Trigger reconnect
└── ui/
    └── MainActivity.kt                ← Minimal UI (status only)
```

---

## Room Database

### Entities

**ScriptEntity** (local script cache)
```kotlin
@Entity(tableName = "scripts")
data class ScriptEntity(
    @PrimaryKey val scriptId: String,
    val name: String,
    val version: Int,
    val jsonDefinition: String,
    val storedAt: Long = System.currentTimeMillis(),
    @ColumnInfo(name = "is_current") val isCurrent: Boolean = false
)
```

**LogEntity** (buffered execution logs)
```kotlin
@Entity(
    tableName = "logs",
    indices = [Index(value = ["deviceId", "timestamp"])]
)
data class LogEntity(
    @PrimaryKey(autoGenerate = true) val id: Long = 0,
    val logId: String,              // Local UUID
    val scriptId: String,
    val stepId: String,
    val level: String,              // "INFO", "WARN", "ERROR"
    val message: String,
    val timestamp: Long,
    @ColumnInfo(name = "synced") var synced: Boolean = false
)
```

**MetricEntity** (buffered device metrics)
```kotlin
@Entity(
    tableName = "metrics",
    indices = [Index(value = ["deviceId", "timestamp"])]
)
data class MetricEntity(
    @PrimaryKey(autoGenerate = true) val id: Long = 0,
    val timestamp: Long,
    val cpuPercent: Double,
    val ramUsedMb: Int,
    val batteryPercent: Int,
    val networkRxBytes: Long,
    val networkTxBytes: Long,
    @ColumnInfo(name = "synced") var synced: Boolean = false
)
```

### DAOs

**ScriptDao**
```kotlin
@Dao
interface ScriptDao {
    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun insertOrUpdate(script: ScriptEntity)

    @Query("SELECT * FROM scripts WHERE scriptId = :scriptId")
    suspend fun getByScriptId(scriptId: String): ScriptEntity?

    @Query("SELECT * FROM scripts WHERE is_current = 1")
    suspend fun getCurrentScript(): ScriptEntity?

    @Update
    suspend fun update(script: ScriptEntity)

    @Query("DELETE FROM scripts WHERE scriptId != :currentId")
    suspend fun deleteOldVersions(currentId: String)
}
```

**LogDao**
```kotlin
@Dao
interface LogDao {
    @Insert
    suspend fun insert(log: LogEntity)

    @Query("SELECT * FROM logs WHERE synced = 0")
    suspend fun getUnsyncedLogs(): List<LogEntity>

    @Query("UPDATE logs SET synced = 1 WHERE logId IN (:logIds)")
    suspend fun markSynced(logIds: List<String>)

    @Query("DELETE FROM logs WHERE timestamp < :olderThan")
    suspend fun deleteOlderThan(olderThan: Long)
}
```

**MetricDao**
```kotlin
@Dao
interface MetricDao {
    @Insert
    suspend fun insert(metric: MetricEntity)

    @Query("SELECT * FROM metrics WHERE synced = 0")
    suspend fun getUnsyncedMetrics(): List<MetricEntity>

    @Query("UPDATE metrics SET synced = 1 WHERE id IN (:ids)")
    suspend fun markSynced(ids: List<Long>)

    @Query("SELECT * FROM metrics ORDER BY timestamp DESC LIMIT 1")
    suspend fun getLatest(): MetricEntity?

    @Query("DELETE FROM metrics WHERE timestamp < :olderThan")
    suspend fun deleteOlderThan(olderThan: Long)
}
```

### Database Context

```kotlin
@Database(
    entities = [ScriptEntity::class, LogEntity::class, MetricEntity::class],
    version = 1,
    exportSchema = false
)
abstract class OrchestratorDatabase : RoomDatabase() {
    abstract fun scriptDao(): ScriptDao
    abstract fun logDao(): LogDao
    abstract fun metricDao(): MetricDao
}
```

---

## MQTT Client

### MqttManager

Lifecycle: connect → subscribe → keep-alive → reconnect on loss

```kotlin
@Singleton
class MqttManager @Inject constructor(
    private val context: Context,
    private val messageHandler: MqttMessageHandler
) {
    private lateinit var client: Mqtt5Client
    private val scope = MainScope()

    fun initialize(deviceId: String, backendHost: String) {
        client = MqttClient.builder()
            .identifier("device-$deviceId")
            .serverHost(backendHost)
            .serverPort(1883)
            .buildAsync()

        scope.launch {
            connectWithRetry()
        }
    }

    private suspend fun connectWithRetry() {
        var backoffMs = 1000
        while (true) {
            try {
                client.connectWith()
                    .cleanStart(false)  // CRITICAL: persist session
                    .sessionExpiryInterval(3600)
                    .keepAlive(60)
                    .send()
                    .await()

                onConnectionSuccess()
                backoffMs = 1000
            } catch (e: Exception) {
                Log.w("MqttManager", "Connect failed, retry in ${backoffMs}ms", e)
                delay(backoffMs)
                backoffMs = minOf(backoffMs * 2, 30000)
            }
        }
    }

    private suspend fun onConnectionSuccess() {
        Log.d("MqttManager", "Connected to broker")

        // Subscribe to command topics
        client.subscribeWith()
            .topicFilter("orchestrator/devices/$deviceId/commands")
            .qos(MqttQos.AT_LEAST_ONCE)
            .send()
            .await()

        client.subscribeWith()
            .topicFilter("orchestrator/broadcast/commands")
            .qos(MqttQos.AT_LEAST_ONCE)
            .send()
            .await()

        // Set up message listener
        client.toAsync().publishes(MqttGlobalPublishFilter.ALL) { publish ->
            scope.launch {
                messageHandler.handle(publish)
            }
        }
    }

    suspend fun publish(topic: String, payload: String, qos: MqttQos = MqttQos.AT_LEAST_ONCE) {
        try {
            client.publishWith()
                .topic(topic)
                .payload(payload.toByteArray())
                .qos(qos)
                .send()
                .await()
        } catch (e: Exception) {
            Log.e("MqttManager", "Publish failed to $topic", e)
        }
    }
}
```

**Key properties:**
- `cleanStart=false` → persist session on broker
- `sessionExpiryInterval=3600` → keep session alive 1 hour
- `keepAlive=60` → heartbeat every 60s
- Auto-reconnect with exponential backoff

---

### MqttMessageHandler

Routes incoming commands to handlers.

```kotlin
@Singleton
class MqttMessageHandler @Inject constructor(
    private val executionEngine: ScriptExecutionEngine,
    private val mqttManager: MqttManager,
    private val logRepository: LogRepository,
    private val scriptRepository: ScriptRepository
) {
    suspend fun handle(publish: MqttPublish) {
        val topic = publish.topic.toString()
        val payload = publish.payloadAsBytes.decodeToString()

        try {
            val envelope = Json.decodeFromString<CommandEnvelope>(payload)

            when (envelope.type) {
                CommandType.ASSIGN_SCRIPT -> handleAssignScript(envelope)
                CommandType.PAUSE_EXECUTION -> handlePause(envelope)
                CommandType.RESUME_EXECUTION -> handleResume(envelope)
                CommandType.ABORT_EXECUTION -> handleAbort(envelope)
                else -> Log.w("MqttHandler", "Unknown command type: ${envelope.type}")
            }

            // ACK the command
            if (envelope.requiresAck) {
                publishAck(envelope.commandId, "RECEIVED")
            }
        } catch (e: Exception) {
            Log.e("MqttHandler", "Failed to handle message", e)
        }
    }

    private suspend fun handleAssignScript(envelope: CommandEnvelope) {
        val scriptData = envelope.payload as? Map<*, *> ?: return
        val scriptId = scriptData["scriptId"] as? String ?: return
        val script = scriptData["script"] as? Map<*, *> ?: return

        // Save to Room DB
        val entity = ScriptEntity(
            scriptId = scriptId,
            name = script["name"] as String,
            version = (script["version"] as? Number)?.toInt() ?: 1,
            jsonDefinition = Json.encodeToString(script),
            isCurrent = true
        )
        scriptRepository.save(entity)

        // Start execution
        executionEngine.start(entity)
        publishAck(envelope.commandId, "EXECUTING")
    }

    private suspend fun handlePause(envelope: CommandEnvelope) {
        executionEngine.pause()
        publishAck(envelope.commandId, "PAUSED")
    }

    private suspend fun handleResume(envelope: CommandEnvelope) {
        executionEngine.resume()
        publishAck(envelope.commandId, "RESUMED")
    }

    private suspend fun handleAbort(envelope: CommandEnvelope) {
        executionEngine.abort()
        publishAck(envelope.commandId, "ABORTED")
    }

    private suspend fun publishAck(commandId: String, status: String) {
        val ackPayload = mapOf(
            "commandId" to commandId,
            "status" to status,
            "timestamp" to System.currentTimeMillis()
        )
        mqttManager.publish(
            "orchestrator/devices/$deviceId/ack",
            Json.encodeToString(ackPayload)
        )
    }
}
```

---

## Script Execution Engine

Core loop. Reads script from Room DB (not memory). Continues offline.

```kotlin
@Singleton
class ScriptExecutionEngine @Inject constructor(
    private val scriptRepository: ScriptRepository,
    private val logRepository: LogRepository,
    private val executors: Map<String, StepExecutor>,
    private val mqttManager: MqttManager
) {
    private val scope = MainScope()
    private var executionJob: Job? = null
    private var executionState = mutableStateOf<ExecutionState>(ExecutionState.IDLE)

    suspend fun start(scriptEntity: ScriptEntity) {
        if (executionJob?.isActive == true) {
            Log.w("ScriptEngine", "Script already running")
            return
        }

        executionJob = scope.launch {
            executeScript(scriptEntity)
        }
    }

    private suspend fun executeScript(scriptEntity: ScriptEntity) {
        try {
            executionState.value = ExecutionState.EXECUTING

            val script = Json.decodeFromString<ScriptDefinition>(scriptEntity.jsonDefinition)
            val loopCount = if (script.loopCount < 0) Int.MAX_VALUE else script.loopCount

            repeat(loopCount) { iteration ->
                Log.d("ScriptEngine", "Loop iteration ${iteration + 1}/$loopCount")

                for (step in script.steps) {
                    if (executionState.value == ExecutionState.PAUSED) {
                        // Wait for resume
                        while (executionState.value == ExecutionState.PAUSED) {
                            delay(100)
                        }
                    }

                    if (executionState.value == ExecutionState.ABORTING) {
                        break
                    }

                    try {
                        executeStep(script.scriptId, step)
                    } catch (e: StepException) {
                        handleStepFailure(step, e)
                        if (step.onFailure == "ABORT_SCRIPT") {
                            break
                        }
                    }
                }

                if (executionState.value == ExecutionState.ABORTING) {
                    break
                }
            }

            executionState.value = ExecutionState.COMPLETED
            Log.d("ScriptEngine", "Script execution completed")

        } catch (e: Exception) {
            Log.e("ScriptEngine", "Script execution failed", e)
            executionState.value = ExecutionState.ERROR
        }
    }

    private suspend fun executeStep(scriptId: String, step: StepDefinition) {
        Log.d("ScriptEngine", "Executing step ${step.stepId}: ${step.type}")

        val executor = executors[step.type]
            ?: throw StepException("No executor for ${step.type}")

        val result = executor.execute(step)

        // Log the result
        logRepository.insert(
            LogEntity(
                logId = UUID.randomUUID().toString(),
                scriptId = scriptId,
                stepId = step.stepId,
                level = if (result.success) "INFO" else "WARN",
                message = result.message,
                timestamp = System.currentTimeMillis(),
                synced = false  // Buffered
            )
        )

        // Publish to MQTT (async, don't block)
        scope.launch {
            try {
                mqttManager.publish(
                    "orchestrator/devices/$deviceId/logs",
                    Json.encodeToString(mapOf(
                        "logId" to result.logId,
                        "scriptId" to scriptId,
                        "stepId" to step.stepId,
                        "level" to (if (result.success) "INFO" else "WARN"),
                        "message" to result.message,
                        "timestamp" to System.currentTimeMillis()
                    ))
                )
            } catch (e: Exception) {
                Log.w("ScriptEngine", "Failed to publish log", e)
            }
        }
    }

    private suspend fun handleStepFailure(step: StepDefinition, e: StepException) {
        when (step.onFailure) {
            "RETRY" -> {
                repeat(step.retryCount) {
                    try {
                        executeStep(step)
                        return
                    } catch (e: Exception) {
                        delay(1000)
                    }
                }
                throw e
            }
            "SKIP" -> {
                Log.w("ScriptEngine", "Step ${step.stepId} failed, skipping")
            }
            "ABORT_SCRIPT" -> throw e
        }
    }

    fun pause() {
        if (executionState.value == ExecutionState.EXECUTING) {
            executionState.value = ExecutionState.PAUSED
        }
    }

    fun resume() {
        if (executionState.value == ExecutionState.PAUSED) {
            executionState.value = ExecutionState.EXECUTING
        }
    }

    fun abort() {
        executionState.value = ExecutionState.ABORTING
        executionJob?.cancel()
        executionState.value = ExecutionState.IDLE
    }
}
```

**Offline property:** Reads script from Room DB every iteration (ScriptEntity.jsonDefinition). If MQTT unavailable, logs buffered (LogEntity.synced=false).

---

## Step Executors

Interface + implementations for each step type.

```kotlin
interface StepExecutor {
    suspend fun execute(step: StepDefinition): StepResult
}

data class StepResult(
    val logId: String,
    val success: Boolean,
    val message: String
)

data class StepException(val message: String) : Exception(message)
```

### LaunchAppExecutor
```kotlin
class LaunchAppExecutor @Inject constructor(
    private val context: Context
) : StepExecutor {
    override suspend fun execute(step: StepDefinition): StepResult {
        val packageName = step.params["packageName"] as? String
            ?: return StepResult("failed", false, "Missing packageName")

        try {
            val intent = context.packageManager.getLaunchIntentForPackage(packageName)
                ?: throw Exception("App not found: $packageName")
            context.startActivity(intent)
            delay(500)  // Wait for app to launch
            return StepResult(UUID.randomUUID().toString(), true, "Launched $packageName")
        } catch (e: Exception) {
            return StepResult(UUID.randomUUID().toString(), false, "Launch failed: ${e.message}")
        }
    }
}
```

### WaitExecutor
```kotlin
class WaitExecutor : StepExecutor {
    override suspend fun execute(step: StepDefinition): StepResult {
        val durationMs = (step.params["durationMs"] as? Number)?.toLong() ?: 0
        delay(durationMs)
        return StepResult(UUID.randomUUID().toString(), true, "Waited ${durationMs}ms")
    }
}
```

### ClickExecutor (AccessibilityService)
```kotlin
class ClickExecutor @Inject constructor(
    private val accessibilityManager: AccessibilityServiceManager
) : StepExecutor {
    override suspend fun execute(step: StepDefinition): StepResult {
        val selector = step.params["selector"] as? String
            ?: return StepResult("failed", false, "Missing selector")

        return try {
            accessibilityManager.clickElement(selector)
            delay(300)
            StepResult(UUID.randomUUID().toString(), true, "Clicked $selector")
        } catch (e: Exception) {
            StepResult(UUID.randomUUID().toString(), false, "Click failed: ${e.message}")
        }
    }
}
```

---

## Foreground Service

Keeps app alive. Android won't kill process.

```kotlin
class AgentForegroundService : Service() {
    private val scope = MainScope()

    override fun onStartCommand(intent: Intent?, flags: Int, startId: Int): Int {
        // Acquire WakeLock
        val wakeLock = (getSystemService(Context.POWER_SERVICE) as PowerManager)
            .newWakeLock(PowerManager.PARTIAL_WAKE_LOCK, "Orchestrator::ScriptExecution")
        wakeLock.acquire(10*60*1000L)  // 10 minutes

        // Start foreground notification
        val notification = NotificationCompat.Builder(this, "orchestrator")
            .setContentTitle("Orchestrator Agent")
            .setContentText("Running script execution")
            .setSmallIcon(R.drawable.ic_launcher)
            .build()

        startForeground(1, notification)

        // Initialize MQTT, start heartbeat, etc.
        scope.launch {
            MqttManager.getInstance().initialize(getDeviceId(), getBackendHost())
        }

        return START_STICKY  // Restart if killed by system
    }

    override fun onDestroy() {
        scope.cancel()
        super.onDestroy()
    }

    override fun onBind(intent: Intent?): IBinder? = null
}
```

---

## Auto-Start & Reconnect

### BootReceiver
```kotlin
class BootReceiver : BroadcastReceiver() {
    override fun onReceive(context: Context, intent: Intent) {
        if (intent.action == Intent.ACTION_BOOT_COMPLETED) {
            Log.d("BootReceiver", "Device rebooted, starting agent")
            val serviceIntent = Intent(context, AgentForegroundService::class.java)
            context.startForegroundService(serviceIntent)
        }
    }
}
```

**Manifest:**
```xml
<receiver android:name=".receivers.BootReceiver">
    <intent-filter>
        <action android:name="android.intent.action.BOOT_COMPLETED" />
    </intent-filter>
</receiver>
<uses-permission android:name="android.permission.RECEIVE_BOOT_COMPLETED" />
```

### NetworkChangeReceiver
```kotlin
class NetworkChangeReceiver : BroadcastReceiver() {
    override fun onReceive(context: Context, intent: Intent) {
        if (intent.action == ConnectivityManager.CONNECTIVITY_ACTION) {
            Log.d("NetworkChangeReceiver", "Network state changed")
            
            val connectivityManager = context.getSystemService(Context.CONNECTIVITY_SERVICE) as ConnectivityManager
            val isOnline = connectivityManager.activeNetworkInfo?.isConnected == true

            if (isOnline) {
                // Trigger MQTT reconnect
                MqttManager.getInstance().reconnect()

                // Flush buffered logs/metrics
                GlobalScope.launch {
                    LogUploadService.flushPending(context)
                    MetricsCollectionService.flush(context)
                }
            }
        }
    }
}
```

---

## Metrics Collection

Samples every 30 seconds.

```kotlin
class MetricsCollectionService @Inject constructor(
    private val metricRepository: MetricRepository,
    private val mqttManager: MqttManager
) {
    fun startCollecting() {
        scope.launch {
            while (isActive) {
                val cpu = getCpuUsage()
                val ram = getRamUsage()
                val battery = getBatteryLevel()

                val metric = MetricEntity(
                    timestamp = System.currentTimeMillis(),
                    cpuPercent = cpu,
                    ramUsedMb = ram,
                    batteryPercent = battery,
                    networkRxBytes = getNetworkRxBytes(),
                    networkTxBytes = getNetworkTxBytes(),
                    synced = false
                )
                metricRepository.insert(metric)

                // Publish to MQTT
                mqttManager.publish(
                    "orchestrator/devices/$deviceId/metrics",
                    Json.encodeToString(metric)
                )

                delay(30000)  // Every 30s
            }
        }
    }

    private fun getCpuUsage(): Double {
        // Read /proc/stat
        return 0.0  // Placeholder
    }

    // ... other collectors ...
}
```

---

## Permissions (AndroidManifest.xml)

```xml
<uses-permission android:name="android.permission.INTERNET" />
<uses-permission android:name="android.permission.WRITE_EXTERNAL_STORAGE" />
<uses-permission android:name="android.permission.READ_EXTERNAL_STORAGE" />
<uses-permission android:name="android.permission.RECEIVE_BOOT_COMPLETED" />
<uses-permission android:name="android.permission.CHANGE_NETWORK_STATE" />
<uses-permission android:name="android.permission.ACCESS_NETWORK_STATE" />
<uses-permission android:name="android.permission.BATTERY_STATS" />
<uses-permission android:name="android.permission.WAKE_LOCK" />

<!-- AccessibilityService (manual grant, one-time) -->
<uses-permission android:name="android.permission.BIND_ACCESSIBILITY_SERVICE" />
```

User grants AccessibilityService permission via Settings → Accessibility once.

---

## Hilt Dependency Injection

### AppModule
```kotlin
@Module
@InstallIn(SingletonComponent::class)
object AppModule {
    @Singleton
    @Provides
    fun provideContext(app: OrchestratorApplication): Context = app.applicationContext

    @Singleton
    @Provides
    fun providePreferences(context: Context): PreferencesManager {
        return PreferencesManager(context.getSharedPreferences("orchestrator", Context.MODE_PRIVATE))
    }
}
```

### MqttModule
```kotlin
@Module
@InstallIn(SingletonComponent::class)
object MqttModule {
    @Singleton
    @Provides
    fun provideMqttManager(context: Context, handler: MqttMessageHandler): MqttManager {
        return MqttManager(context, handler)
    }

    @Singleton
    @Provides
    fun provideMqttHandler(
        engine: ScriptExecutionEngine,
        logRepo: LogRepository,
        scriptRepo: ScriptRepository
    ): MqttMessageHandler {
        return MqttMessageHandler(engine, logRepo, scriptRepo)
    }
}
```

### DatabaseModule
```kotlin
@Module
@InstallIn(SingletonComponent::class)
object DatabaseModule {
    @Singleton
    @Provides
    fun provideDatabase(context: Context): OrchestratorDatabase {
        return Room.databaseBuilder(
            context,
            OrchestratorDatabase::class.java,
            "orchestrator.db"
        ).build()
    }

    @Singleton
    @Provides
    fun provideScriptDao(db: OrchestratorDatabase): ScriptDao = db.scriptDao()

    @Singleton
    @Provides
    fun provideLogDao(db: OrchestratorDatabase): LogDao = db.logDao()

    @Singleton
    @Provides
    fun provideMetricDao(db: OrchestratorDatabase): MetricDao = db.metricDao()
}
```
