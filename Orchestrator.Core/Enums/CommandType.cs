namespace Orchestrator.Core.Enums;

public enum CommandType
{
    AssignScript,
    PauseExecution,
    ResumeExecution,
    AbortExecution,
    PushScriptStore,
    RequestStatus,
    UpdateConfig,
    RebootDevice
}
