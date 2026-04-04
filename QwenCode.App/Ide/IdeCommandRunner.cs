using System.Diagnostics;

namespace QwenCode.App.Ide;

public sealed class IdeCommandRunner : IIdeCommandRunner
{
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
