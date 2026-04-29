using Microsoft.AspNetCore.Mvc;
using Orchestrator.Core.Interfaces;
using Orchestrator.Core.Models;

namespace Orchestrator.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MetricsController : ControllerBase
{
    private readonly IMetricRepository _metricRepository;
    private readonly ILogger<MetricsController> _logger;

    public MetricsController(IMetricRepository metricRepository, ILogger<MetricsController> logger)
    {
        _metricRepository = metricRepository;
        _logger = logger;
    }

    [HttpGet("{deviceId}/latest")]
    public async Task<ActionResult<DeviceMetric>> GetLatest(Guid deviceId)
    {
        var metric = await _metricRepository.GetLatestByDeviceAsync(deviceId);
        if (metric == null)
            return NotFound();

        return Ok(metric);
    }

    [HttpGet]
    public async Task<ActionResult<List<DeviceMetric>>> Get(
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
            var metrics = await _metricRepository.GetByDeviceAndTimeAsync(deviceId.Value, from.Value, to.Value);
            return Ok(metrics.Skip((page - 1) * pageSize).Take(pageSize).ToList());
        }
        else
        {
            var metrics = await _metricRepository.GetByDeviceAsync(deviceId.Value, pageSize * page);
            return Ok(metrics.Skip((page - 1) * pageSize).Take(pageSize).ToList());
        }
    }
}
