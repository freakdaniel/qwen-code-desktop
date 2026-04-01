using QwenCode.App.Models;

namespace QwenCode.App.Runtime;

public interface ISlashCommandRuntime
{
    QwenResolvedCommand? TryResolve(
        SourceMirrorPaths paths,
        string prompt,
        string workingDirectory);
}
