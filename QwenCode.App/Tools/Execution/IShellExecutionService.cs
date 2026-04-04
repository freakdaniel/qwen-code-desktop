using QwenCode.App.Models;

namespace QwenCode.App.Tools;

public interface IShellExecutionService
{
    Task<ShellCommandExecutionResult> ExecuteAsync(
        ShellCommandRequest request,
        CancellationToken cancellationToken = default);
}
