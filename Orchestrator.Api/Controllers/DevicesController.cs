using Microsoft.AspNetCore.Mvc;
using Orchestrator.Application.DTOs;
using Orchestrator.Core.Enums;
using Orchestrator.Core.Interfaces;
using Orchestrator.Core.Models;

namespace Orchestrator.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DevicesController : ControllerBase
{
    private readonly IDeviceRepository _deviceRepository;
    private readonly ILogger<DevicesController> _logger;

    public DevicesController(IDeviceRepository deviceRepository, ILogger<DevicesController> logger)
    {
        _deviceRepository = deviceRepository;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<List<DeviceDto>>> GetAll()
    {
        var devices = await _deviceRepository.GetAllAsync();
        var dtos = devices.Select(d => new DeviceDto
        {
            Id = d.Id,
            DeviceSerial = d.DeviceSerial,
            FriendlyName = d.FriendlyName,
            AndroidVersion = d.AndroidVersion,
            LastSeen = d.LastSeen,
            State = d.State.ToString(),
            CurrentScriptId = d.CurrentScriptId
        }).ToList();

        return Ok(dtos);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<DeviceDto>> GetById(Guid id)
    {
        var device = await _deviceRepository.GetByIdAsync(id);
        if (device == null)
            return NotFound();

        var dto = new DeviceDto
        {
            Id = device.Id,
            DeviceSerial = device.DeviceSerial,
            FriendlyName = device.FriendlyName,
            AndroidVersion = device.AndroidVersion,
            LastSeen = device.LastSeen,
            State = device.State.ToString(),
            CurrentScriptId = device.CurrentScriptId
        };

        return Ok(dto);
    }

    [HttpPost("register")]
    public async Task<ActionResult<DeviceDto>> Register([FromBody] RegisterDeviceRequest request)
    {
        var existing = await _deviceRepository.GetBySerialAsync(request.DeviceSerial);
        if (existing != null)
            return BadRequest("Device already registered");

        var device = new Device
        {
            DeviceSerial = request.DeviceSerial,
            FriendlyName = request.FriendlyName,
            AndroidVersion = request.AndroidVersion,
            State = DeviceState.Online,
            LastSeen = DateTime.UtcNow
        };

        await _deviceRepository.CreateAsync(device);
        _logger.LogInformation("Device {Serial} registered", request.DeviceSerial);

        var dto = new DeviceDto
        {
            Id = device.Id,
            DeviceSerial = device.DeviceSerial,
            FriendlyName = device.FriendlyName,
            AndroidVersion = device.AndroidVersion,
            LastSeen = device.LastSeen,
            State = device.State.ToString(),
            CurrentScriptId = device.CurrentScriptId
        };

        return Created($"api/devices/{device.Id}", dto);
    }

    [HttpPut("{id}/name")]
    public async Task<ActionResult<DeviceDto>> UpdateName(Guid id, [FromBody] UpdateDeviceNameRequest request)
    {
        var device = await _deviceRepository.GetByIdAsync(id);
        if (device == null)
            return NotFound();

        device.FriendlyName = request.FriendlyName;
        await _deviceRepository.UpdateAsync(device);

        var dto = new DeviceDto
        {
            Id = device.Id,
            DeviceSerial = device.DeviceSerial,
            FriendlyName = device.FriendlyName,
            AndroidVersion = device.AndroidVersion,
            LastSeen = device.LastSeen,
            State = device.State.ToString(),
            CurrentScriptId = device.CurrentScriptId
        };

        return Ok(dto);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var result = await _deviceRepository.DeleteAsync(id);
        if (!result)
            return NotFound();

        _logger.LogInformation("Device {Id} deleted", id);
        return NoContent();
    }
}

public class RegisterDeviceRequest
{
    public string DeviceSerial { get; set; } = string.Empty;
    public string FriendlyName { get; set; } = string.Empty;
    public string AndroidVersion { get; set; } = string.Empty;
}

public class UpdateDeviceNameRequest
{
    public string FriendlyName { get; set; } = string.Empty;
}
