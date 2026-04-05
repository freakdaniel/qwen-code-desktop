using QwenCode.App.Models;

namespace QwenCode.App.Runtime;

/// <summary>
/// Defines the contract for Slash Command Runtime
/// </summary>
public interface ISlashCommandRuntime
{
    /// <summary>
    /// Attempts to resolve
    /// </summary>
    /// <param name="paths">The paths to process</param>
    /// <param name="prompt">The prompt content</param>
    /// <param name="workingDirectory">The working directory</param>
    /// <returns>The resulting resolved command?</returns>
    ResolvedCommand? TryResolve(
        WorkspacePaths paths,
        string prompt,
        string workingDirectory);
}
