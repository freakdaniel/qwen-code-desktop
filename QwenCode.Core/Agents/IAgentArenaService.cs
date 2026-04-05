using System.Text.Json;
using QwenCode.App.Models;
using QwenCode.App.Runtime;

namespace QwenCode.App.Agents;

public interface IAgentArenaService
{
    Task<NativeToolExecutionResult> ExecuteAsync(
        WorkspacePaths paths,
        QwenRuntimeProfile runtimeProfile,
        JsonElement arguments,
        string approvalState,
        Action<AssistantRuntimeEvent>? eventSink = null,
        CancellationToken cancellationToken = default);
}
