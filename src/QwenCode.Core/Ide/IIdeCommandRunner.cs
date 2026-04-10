namespace QwenCode.Core.Ide;

/// <summary>
/// Defines the contract for Ide Command Runner
/// </summary>
public interface IIdeCommandRunner
{
    /// <summary>
    /// Executes run async
    /// </summary>
    /// <param name="fileName">The file name</param>
    /// <param name="arguments">The arguments</param>
    /// <param name="useShellExecute">The use shell execute</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that resolves to ide command result</returns>
    Task<IdeCommandResult> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        bool useShellExecute = false,
        CancellationToken cancellationToken = default);
}
