using Microsoft.AspNetCore.Mvc;
using Orchestrator.Application.DTOs;
using Orchestrator.Core.Interfaces;

namespace Orchestrator.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrchestrationController : ControllerBase
{
    private readonly IOrchestrationService _orchestrationService;
    private readonly ILogger<OrchestrationController> _logger;

    public OrchestrationController(IOrchestrationService orchestrationService, ILogger<OrchestrationController> logger)
    {
        _orchestrationService = orchestrationService;
        _logger = logger;
    }

    [HttpPost("assign")]
    public async Task<IActionResult> Assign([FromBody] AssignScriptRequest request)
    {
        await _orchestrationService.AssignScriptAsync(request.DeviceId, request.ScriptId);
        _logger.LogInformation("Script assignment requested: Device={DeviceId}, Script={ScriptId}",
            request.DeviceId, request.ScriptId);
        return Accepted();
    }

    [HttpPost("broadcast")]
    public async Task<IActionResult> Broadcast([FromBody] BroadcastScriptRequest request)
    {
        await _orchestrationService.BroadcastScriptAsync(request.ScriptId);
        _logger.LogInformation("Broadcast requested: Script={ScriptId}", request.ScriptId);
        return Accepted();
    }

    [HttpPost("pause")]
    public async Task<IActionResult> Pause([FromBody] DeviceCommandRequest request)
    {
        await _orchestrationService.PauseExecutionAsync(request.DeviceId);
        _logger.LogInformation("Pause requested: Device={DeviceId}", request.DeviceId);
        return Accepted();
    }

    [HttpPost("resume")]
    public async Task<IActionResult> Resume([FromBody] DeviceCommandRequest request)
    {
        await _orchestrationService.ResumeExecutionAsync(request.DeviceId);
        _logger.LogInformation("Resume requested: Device={DeviceId}", request.DeviceId);
        return Accepted();
    }

    [HttpPost("abort")]
    public async Task<IActionResult> Abort([FromBody] DeviceCommandRequest request)
    {
        await _orchestrationService.AbortExecutionAsync(request.DeviceId);
        _logger.LogInformation("Abort requested: Device={DeviceId}", request.DeviceId);
        return Accepted();
    }
}
