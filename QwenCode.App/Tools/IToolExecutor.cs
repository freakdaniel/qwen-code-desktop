using QwenCode.App.Models;

namespace QwenCode.App.Tools;

public interface IToolExecutor
{
    QwenNativeToolHostSnapshot Inspect(SourceMirrorPaths paths);

    Task<QwenNativeToolExecutionResult> ExecuteAsync(
        SourceMirrorPaths paths,
        ExecuteNativeToolRequest request,
        CancellationToken cancellationToken = default);
}
