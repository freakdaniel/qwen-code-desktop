using QwenCode.App.Models;
using QwenCode.App.Tools;

namespace QwenCode.App.Runtime;

public sealed class NonInteractiveToolExecutor(IToolExecutor toolExecutor) : INonInteractiveToolExecutor
{
    public Task<NativeToolExecutionResult> ExecuteAsync(
        AssistantTurnRequest request,
        AssistantToolCall toolCall,
        Action<AssistantRuntimeEvent>? eventSink = null,
        CancellationToken cancellationToken = default) =>
        toolExecutor.ExecuteAsync(
            new WorkspacePaths
            {
                WorkspaceRoot = request.RuntimeProfile.ProjectRoot
            },
            new ExecuteNativeToolRequest
            {
                ToolName = toolCall.ToolName,
                ArgumentsJson = string.IsNullOrWhiteSpace(toolCall.ArgumentsJson) ? "{}" : toolCall.ArgumentsJson,
                ApproveExecution = false
            },
            eventSink,
            cancellationToken);
}
