namespace QwenCode.App.Infrastructure;

/// <summary>
/// Defines the contract for Git Cli Service
/// </summary>
public interface IGitCliService
{
    /// <summary>
    /// Executes run
    /// </summary>
    /// <param name="workingDirectory">The working directory</param>
    /// <param name="arguments">The arguments</param>
    /// <returns>The resulting git command result</returns>
    GitCommandResult Run(string workingDirectory, params string[] arguments);
}
