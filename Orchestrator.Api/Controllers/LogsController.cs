using Microsoft.AspNetCore.Mvc;
using Orchestrator.Core.Interfaces;
using Orchestrator.Core.Models;

namespace Orchestrator.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LogsController : ControllerBase
{
    private readonly ILogRepository _logRepository;
    private readonly ILogger<LogsController> _logger;

    public LogsController(ILogRepository logRepository, ILogger<LogsController> logger)
    {
        _logRepository = logRepository;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<List<DeviceLog>>> Get(
        [FromQuery] Guid? deviceId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 100)
    {
        if (deviceId == null)
        {
            return BadRequest("deviceId is required");
        }

        if (from.HasValue && to.HasValue)
        {
            var logs = await _logRepository.GetByDeviceAndTimeAsync(deviceId.Value, from.Value, to.Value);
            return Ok(logs.Skip((page - 1) * pageSize).Take(pageSize).ToList());
        }
        else
        {
            var logs = await _logRepository.GetByDeviceAsync(deviceId.Value, pageSize * page);
            return Ok(logs.Skip((page - 1) * pageSize).Take(pageSize).ToList());
        }
    }

    [HttpPost("batch")]
    public async Task<IActionResult> UploadBatch([FromBody] List<DeviceLog> logs)
    {
        if (!logs.Any())
        {
            return BadRequest("No logs provided");
        }

        foreach (var log in logs)
        {
            log.Synced = false;
            await _logRepository.CreateAsync(log);
        }

        _logger.LogInformation("Batch upload: {Count} logs from device {DeviceId}", logs.Count, logs.FirstOrDefault()?.DeviceId);
        return Accepted();
    }
}
