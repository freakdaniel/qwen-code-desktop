namespace QwenCode.Tests.Tools;

public sealed class ShellExecutionServiceTests
{
    [Fact]
    public async Task ShellExecutionService_ExecuteAsync_UsesRequestedWorkingDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-shell-service-{Guid.NewGuid():N}");
        var workingDirectory = Path.Combine(root, "workspace");
        Directory.CreateDirectory(workingDirectory);

        try
        {
            var service = new ShellExecutionService();
            var command = OperatingSystem.IsWindows() ? "echo %CD%" : "pwd";

            var result = await service.ExecuteAsync(
                new ShellCommandRequest
                {
                    Command = command,
                    WorkingDirectory = workingDirectory
                });

            Assert.Equal(0, result.ExitCode);
            Assert.Equal(workingDirectory, result.WorkingDirectory);
            Assert.Contains(Path.GetFullPath(workingDirectory), result.Output, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ShellExecutionService_ExecuteAsync_CapturesStdoutAndStderr()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-shell-output-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var service = new ShellExecutionService();
            var command = OperatingSystem.IsWindows()
                ? "echo hello-from-stdout & echo hello-from-stderr 1>&2"
                : "printf 'hello-from-stdout\\n'; printf 'hello-from-stderr\\n' 1>&2";

            var result = await service.ExecuteAsync(
                new ShellCommandRequest
                {
                    Command = command,
                    WorkingDirectory = root
                });

            Assert.Equal(0, result.ExitCode);
            Assert.False(result.TimedOut);
            Assert.False(result.Cancelled);
            Assert.Contains("hello-from-stdout", result.Output);
            Assert.Contains("hello-from-stderr", result.Output);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ShellExecutionService_ExecuteAsync_ReturnsTimeoutResult()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-shell-timeout-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var service = new ShellExecutionService();
            var command = OperatingSystem.IsWindows()
                ? "powershell -NoProfile -Command \"Start-Sleep -Seconds 5\""
                : "sleep 5";

            var result = await service.ExecuteAsync(
                new ShellCommandRequest
                {
                    Command = command,
                    WorkingDirectory = root,
                    Timeout = TimeSpan.FromMilliseconds(100)
                });

            Assert.True(result.TimedOut);
            Assert.False(result.Cancelled);
            Assert.Equal(-1, result.ExitCode);
            Assert.Contains("timed out", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
