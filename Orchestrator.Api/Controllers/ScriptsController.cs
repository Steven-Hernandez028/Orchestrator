using Microsoft.AspNetCore.Mvc;
using Orchestrator.Application.DTOs;
using Orchestrator.Core.Interfaces;
using Orchestrator.Core.Models;

namespace Orchestrator.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ScriptsController : ControllerBase
{
    private readonly IScriptRepository _scriptRepository;
    private readonly ILogger<ScriptsController> _logger;

    public ScriptsController(IScriptRepository scriptRepository, ILogger<ScriptsController> logger)
    {
        _scriptRepository = scriptRepository;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<List<ScriptDto>>> GetAll()
    {
        var scripts = await _scriptRepository.GetAllAsync();
        var dtos = scripts.Select(s => new ScriptDto
        {
            Id = s.Id,
            Name = s.Name,
            Version = s.Version,
            JsonDefinition = s.JsonDefinition,
            CreatedAt = s.CreatedAt,
            UpdatedAt = s.UpdatedAt
        }).ToList();

        return Ok(dtos);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ScriptDto>> GetById(Guid id)
    {
        var script = await _scriptRepository.GetByIdAsync(id);
        if (script == null)
            return NotFound();

        var dto = new ScriptDto
        {
            Id = script.Id,
            Name = script.Name,
            Version = script.Version,
            JsonDefinition = script.JsonDefinition,
            CreatedAt = script.CreatedAt,
            UpdatedAt = script.UpdatedAt
        };

        return Ok(dto);
    }

    [HttpPost]
    public async Task<ActionResult<ScriptDto>> Create([FromBody] CreateScriptRequest request)
    {
        var script = new Script
        {
            Name = request.Name,
            JsonDefinition = request.JsonDefinition
        };

        await _scriptRepository.CreateAsync(script);
        _logger.LogInformation("Script {Name} created with ID {Id}", request.Name, script.Id);

        var dto = new ScriptDto
        {
            Id = script.Id,
            Name = script.Name,
            Version = script.Version,
            JsonDefinition = script.JsonDefinition,
            CreatedAt = script.CreatedAt,
            UpdatedAt = script.UpdatedAt
        };

        return Created($"api/scripts/{script.Id}", dto);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<ScriptDto>> Update(Guid id, [FromBody] UpdateScriptRequest request)
    {
        var script = await _scriptRepository.GetByIdAsync(id);
        if (script == null)
            return NotFound();

        script.Name = request.Name;
        script.JsonDefinition = request.JsonDefinition;

        await _scriptRepository.UpdateAsync(script);
        _logger.LogInformation("Script {Id} updated to version {Version}", id, script.Version);

        var dto = new ScriptDto
        {
            Id = script.Id,
            Name = script.Name,
            Version = script.Version,
            JsonDefinition = script.JsonDefinition,
            CreatedAt = script.CreatedAt,
            UpdatedAt = script.UpdatedAt
        };

        return Ok(dto);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var result = await _scriptRepository.DeleteAsync(id);
        if (!result)
            return NotFound();

        _logger.LogInformation("Script {Id} deleted", id);
        return NoContent();
    }
}
