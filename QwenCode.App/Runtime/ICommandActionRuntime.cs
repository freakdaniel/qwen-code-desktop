using QwenCode.App.Models;

namespace QwenCode.App.Runtime;

public interface ICommandActionRuntime
{
    Task<CommandInvocationResult?> TryInvokeAsync(
        SourceMirrorPaths paths,
        string prompt,
        string workingDirectory,
        CancellationToken cancellationToken = default);
}
