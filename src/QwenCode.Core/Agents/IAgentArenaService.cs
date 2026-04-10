using System.Text.Json;
using QwenCode.Core.Models;
using QwenCode.Core.Runtime;

namespace QwenCode.Core.Agents;

/// <summary>
/// Defines the contract for Agent Arena Service
/// </summary>
public interface IAgentArenaService
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
