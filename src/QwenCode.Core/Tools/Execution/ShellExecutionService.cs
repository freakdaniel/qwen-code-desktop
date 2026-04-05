using System.Diagnostics;
using System.Text;
using QwenCode.App.Models;

namespace QwenCode.App.Tools;

/// <summary>
/// Represents the Shell Execution Service
/// </summary>
public sealed class ShellExecutionService : IShellExecutionService
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Executes async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that resolves to shell command execution result</returns>
    public async Task<ShellCommandExecutionResult> ExecuteAsync(
        ShellCommandRequest request,
        CancellationToken cancellationToken = default)
    {
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(request.Timeout ?? DefaultTimeout);

        using var process = new Process
        {
            StartInfo = BuildStartInfo(request)
        };

        try
        {
            process.Start();
        }
        catch (Exception exception)
        {
            return new ShellCommandExecutionResult
            {
                WorkingDirectory = request.WorkingDirectory,
                Output = string.Empty,
                ErrorMessage = exception.Message,
                ExitCode = -1,
                TimedOut = false,
                Cancelled = false
            };
        }

        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();

        try
        {
            await process.WaitForExitAsync(timeoutSource.Token);
            var stdout = await stdoutTask;
            var stderr = await stderrTask;

            return new ShellCommandExecutionResult
            {
                WorkingDirectory = request.WorkingDirectory,
                Output = CombineOutput(stdout, stderr),
                ErrorMessage = process.ExitCode == 0 ? string.Empty : "Shell command exited with a non-zero status.",
                ExitCode = process.ExitCode,
                TimedOut = false,
                Cancelled = false
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            TryKillProcess(process);
            await AwaitSilently(stdoutTask, stderrTask);

            return new ShellCommandExecutionResult
            {
                WorkingDirectory = request.WorkingDirectory,
                Output = string.Empty,
                ErrorMessage = $"Shell command timed out after {(request.Timeout ?? DefaultTimeout).TotalMilliseconds:0} ms.",
                ExitCode = -1,
                TimedOut = true,
                Cancelled = false
            };
        }
        catch (OperationCanceledException)
        {
            TryKillProcess(process);
            await AwaitSilently(stdoutTask, stderrTask);

            return new ShellCommandExecutionResult
            {
                WorkingDirectory = request.WorkingDirectory,
                Output = string.Empty,
                ErrorMessage = "Shell command cancelled.",
                ExitCode = -1,
                TimedOut = false,
                Cancelled = true
            };
        }
    }

    private static ProcessStartInfo BuildStartInfo(ShellCommandRequest request)
    {
        var startInfo = OperatingSystem.IsWindows()
            ? new ProcessStartInfo("cmd.exe", $"/d /s /c \"{request.Command}\"")
            : new ProcessStartInfo("/bin/bash", $"-lc \"{request.Command.Replace("\"", "\\\"", StringComparison.Ordinal)}\"");

        startInfo.WorkingDirectory = request.WorkingDirectory;
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;
        startInfo.UseShellExecute = false;
        startInfo.CreateNoWindow = true;
        startInfo.StandardOutputEncoding = Encoding.UTF8;
        startInfo.StandardErrorEncoding = Encoding.UTF8;
        return startInfo;
    }

    private static string CombineOutput(string stdout, string stderr)
    {
        if (string.IsNullOrWhiteSpace(stderr))
        {
            return stdout.Trim();
        }

        if (string.IsNullOrWhiteSpace(stdout))
        {
            return stderr.Trim();
        }

        return $"{stdout}{Environment.NewLine}{stderr}".Trim();
    }

    private static void TryKillProcess(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // Best-effort cleanup only.
        }
    }

    private static async Task AwaitSilently(params Task[] tasks)
    {
        try
        {
            await Task.WhenAll(tasks);
        }
        catch
        {
            // Ignore cleanup-time failures after cancellation or timeout.
        }
    }
}
