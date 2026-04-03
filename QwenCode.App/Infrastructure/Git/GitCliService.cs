using System.Diagnostics;

namespace QwenCode.App.Infrastructure;

public sealed class GitCliService : IGitCliService
{
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(8);

    public GitCommandResult Run(string workingDirectory, params string[] arguments)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = BuildStartInfo(workingDirectory, arguments)
            };

            process.Start();
            var standardOutputTask = process.StandardOutput.ReadToEndAsync();
            var standardErrorTask = process.StandardError.ReadToEndAsync();

            if (!process.WaitForExit((int)CommandTimeout.TotalMilliseconds))
            {
                TryKill(process);
                return new GitCommandResult
                {
                    Success = false,
                    ExitCode = -1,
                    StandardError = "Timed out while waiting for git to exit."
                };
            }

            Task.WaitAll(standardOutputTask, standardErrorTask);

            return new GitCommandResult
            {
                Success = process.ExitCode == 0,
                ExitCode = process.ExitCode,
                StandardOutput = standardOutputTask.Result,
                StandardError = standardErrorTask.Result
            };
        }
        catch (Exception exception)
        {
            return new GitCommandResult
            {
                Success = false,
                ExitCode = -1,
                StandardError = exception.Message
            };
        }
    }

    private static ProcessStartInfo BuildStartInfo(string workingDirectory, IReadOnlyList<string> arguments)
    {
        var startInfo = new ProcessStartInfo("git")
        {
            WorkingDirectory = Directory.Exists(workingDirectory)
                ? workingDirectory
                : Environment.CurrentDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return startInfo;
    }

    private static void TryKill(Process process)
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
            // Best effort cleanup.
        }
    }
}
