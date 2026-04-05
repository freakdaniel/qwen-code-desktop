using System.Text.Json;
using QwenCode.App.Compatibility;
using QwenCode.App.Models;
using QwenCode.App.Runtime;

namespace QwenCode.App.Agents;

/// <summary>
/// Defines the contract for Subagent Coordinator
/// </summary>
public interface ISubagentCoordinator
{
    /// <summary>
    /// Executes async
    /// </summary>
    /// <param name="paths">The paths to process</param>
    /// <param name="runtimeProfile">The runtime profile</param>
    /// <param name="arguments">The arguments</param>
    /// <param name="approvalState">The approval state</param>
    /// <param name="eventSink">The optional event sink</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that resolves to native tool execution result</returns>
    Task<NativeToolExecutionResult> ExecuteAsync(
        WorkspacePaths paths,
        QwenRuntimeProfile runtimeProfile,
        JsonElement arguments,
        string approvalState,
        Action<AssistantRuntimeEvent>? eventSink = null,
        CancellationToken cancellationToken = default);
}
