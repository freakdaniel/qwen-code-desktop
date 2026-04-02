using System.Text.Json;
using QwenCode.App.Compatibility;
using QwenCode.App.Models;

namespace QwenCode.App.Agents;

public interface ISubagentCoordinator
{
    Task<NativeToolExecutionResult> ExecuteAsync(
        WorkspacePaths paths,
        QwenRuntimeProfile runtimeProfile,
        JsonElement arguments,
        string approvalState,
        CancellationToken cancellationToken = default);
}
