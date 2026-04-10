using System.Diagnostics;

namespace QwenCode.Core.Ide;

/// <summary>
/// Represents the Ide Command Runner
/// </summary>
public sealed class IdeCommandRunner : IIdeCommandRunner
{
    /// <summary>
    /// Executes run async
    /// </summary>
    /// <param name="fileName">The file name</param>
    /// <param name="arguments">The arguments</param>
    /// <param name="useShellExecute">The use shell execute</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that resolves to ide command result</returns>
    public async Task<IdeCommandResult> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        bool useShellExecute = false,
        CancellationToken cancellationToken = default)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            UseShellExecute = useShellExecute,
            RedirectStandardOutput = !useShellExecute,
            RedirectStandardError = !useShellExecute,
            CreateNoWindow = true
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };
        process.Start();
        var stdout = useShellExecute ? string.Empty : await process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderr = useShellExecute ? string.Empty : await process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        return new IdeCommandResult
        {
            ExitCode = process.ExitCode,
            StandardOutput = stdout,
            StandardError = stderr
        };
    }
}
