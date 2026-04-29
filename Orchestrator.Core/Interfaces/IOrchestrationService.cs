using Orchestrator.Core.Models;

namespace Orchestrator.Core.Interfaces;

public interface IOrchestrationService
{
    Task AssignScriptAsync(Guid deviceId, Guid scriptId);
    Task BroadcastScriptAsync(Guid scriptId);
    Task PauseExecutionAsync(Guid deviceId);
    Task ResumeExecutionAsync(Guid deviceId);
    Task AbortExecutionAsync(Guid deviceId);
    Task HandleDeviceStatusAsync(Guid deviceId, string statusPayload);
    Task HandleAckAsync(string commandId);
}
