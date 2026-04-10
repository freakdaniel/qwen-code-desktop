using QwenCode.Core.Models;
using QwenCode.Core.Runtime;

namespace QwenCode.Core.Tools;

/// <summary>
/// Defines the contract for Tool Executor
/// </summary>
public interface IToolExecutor
{
    /// <summary>
    /// Executes inspect
    /// </summary>
    /// <param name="paths">The paths to process</param>
    /// <returns>The resulting native tool host snapshot</returns>
    NativeToolHostSnapshot Inspect(WorkspacePaths paths);

    /// <summary>
    /// Executes async
    /// </summary>
    /// <param name="paths">The paths to process</param>
    /// <param name="request">The request payload</param>
    /// <param name="eventSink">The optional event sink</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that resolves to native tool execution result</returns>
    Task<NativeToolExecutionResult> ExecuteAsync(
        WorkspacePaths paths,
        ExecuteNativeToolRequest request,
        Action<AssistantRuntimeEvent>? eventSink = null,
        CancellationToken cancellationToken = default);
}
