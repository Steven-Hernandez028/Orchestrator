namespace Orchestrator.Application.DTOs;

public class CreateScriptRequest
{
    public string Name { get; set; } = string.Empty;
    public string JsonDefinition { get; set; } = string.Empty;
}

public class UpdateScriptRequest
{
    public string Name { get; set; } = string.Empty;
    public string JsonDefinition { get; set; } = string.Empty;
}
