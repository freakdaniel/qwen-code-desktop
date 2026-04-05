using QwenCode.App.Models;

namespace QwenCode.App.Runtime;

public interface ISlashCommandRuntime
{
    ResolvedCommand? TryResolve(
        WorkspacePaths paths,
        string prompt,
        string workingDirectory);
}
