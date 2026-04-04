using System.Text.Json;
using QwenCode.App.Ide;

namespace QwenCode.Tests.Ide;

public sealed class IdeBackendServiceTests
{
    [Fact]
    public void IdeDetectionService_Detect_RecognizesCursorAndVscodeFork()
    {
        var service = new IdeDetectionService();

        var cursor = service.Detect(
            "C:\\Tools\\Code.exe",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["TERM_PROGRAM"] = "vscode",
                ["CURSOR_TRACE_ID"] = "trace-1"
            });
        var fork = service.Detect(
            "C:\\Tools\\Fork.exe",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["TERM_PROGRAM"] = "vscode"
            });

        Assert.NotNull(cursor);
        Assert.Equal("cursor", cursor!.Name);
        Assert.NotNull(fork);
        Assert.Equal("vscodefork", fork!.Name);
    }

    [Fact]
    public void IdeContextService_Normalize_TruncatesAndKeepsSingleActiveFile()
    {
        var service = new IdeContextService();
        var snapshot = service.Normalize(new IdeContextSnapshot
        {
            OpenFiles =
            [
                new IdeOpenFile
                {
                    Path = "b.cs",
                    Timestamp = 2,
                    IsActive = true,
                    SelectedText = new string('a', IdeContextService.MaxSelectedTextLength + 25)
                },
                new IdeOpenFile
                {
                    Path = "a.cs",
                    Timestamp = 1,
                    IsActive = true,
                    SelectedText = "stale"
                }
            ],
            IsTrusted = true
        });

        Assert.Equal(2, snapshot.OpenFiles.Count);
        Assert.True(snapshot.OpenFiles[0].IsActive);
        Assert.Contains("[TRUNCATED]", snapshot.OpenFiles[0].SelectedText);
        Assert.False(snapshot.OpenFiles[1].IsActive);
        Assert.Equal(string.Empty, snapshot.OpenFiles[1].SelectedText);
    }

    [Fact]
    public void IdeBackendService_Inspect_ReadsNewestValidLockFile()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-ide-inspect-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var homeRoot = Path.Combine(root, "home");
            var workspaceRoot = Path.Combine(root, "workspace");
            var ideRoot = Path.Combine(homeRoot, ".qwen", "ide");
            Directory.CreateDirectory(ideRoot);
            Directory.CreateDirectory(workspaceRoot);

            var validLock = Path.Combine(ideRoot, "3000.lock");
            File.WriteAllText(
                validLock,
                JsonSerializer.Serialize(new
                {
                    port = "3000",
                    authToken = "secret",
                    workspacePath = workspaceRoot,
                    availableTools = new[] { "openDiff", "closeDiff" },
                    ideInfo = new
                    {
                        name = "cursor",
                        displayName = "Cursor"
                    },
                    ppid = 123
                }));
            File.SetLastWriteTimeUtc(validLock, DateTime.UtcNow);

            var backend = new IdeBackendService(
                new FakeDesktopEnvironmentPaths(homeRoot, null, workspaceRoot, workspaceRoot),
                new IdeDetectionService(),
                new IdeContextService(),
                new IdeInstallerService(new FakeIdeCommandRunner(), new FakeDesktopEnvironmentPaths(homeRoot, null, workspaceRoot, workspaceRoot)),
                new FakeIdeProcessProbe(true));

            var snapshot = backend.Inspect(workspaceRoot, "code");

            Assert.Equal("connected", snapshot.Status);
            Assert.Equal("3000", snapshot.Port);
            Assert.Equal("Cursor", snapshot.Ide?.DisplayName);
            Assert.True(snapshot.SupportsDiff);
            Assert.Equal("***", snapshot.AuthToken);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task IdeInstallerService_InstallCompanionAsync_UsesResolvedCommand()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-ide-install-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var homeRoot = Path.Combine(root, "home");
            var codeCmd = Path.Combine(homeRoot, "AppData", "Local", "Programs", "Microsoft VS Code", "bin", "code.cmd");
            Directory.CreateDirectory(Path.GetDirectoryName(codeCmd)!);
            File.WriteAllText(codeCmd, "@echo off");

            var runner = new FakeIdeCommandRunner();
            var installer = new IdeInstallerService(
                runner,
                new FakeDesktopEnvironmentPaths(homeRoot, null, homeRoot, homeRoot));

            var result = await installer.InstallCompanionAsync(new IdeInfo
            {
                Name = "vscode",
                DisplayName = "VS Code"
            });

            Assert.True(result.Success);
            Assert.Equal(codeCmd, result.CommandPath);
            Assert.Contains("installed successfully", result.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Equal("code.cmd", Path.GetFileName(runner.Invocations.Single().FileName));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void IdeBackendService_Inspect_UsesEnvironmentFallbackWhenLockFileIsMissing()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-ide-env-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        var previousPort = Environment.GetEnvironmentVariable("QWEN_CODE_IDE_SERVER_PORT");
        var previousWorkspace = Environment.GetEnvironmentVariable("QWEN_CODE_IDE_WORKSPACE_PATH");
        var previousAuth = Environment.GetEnvironmentVariable("QWEN_CODE_IDE_AUTH_TOKEN");

        try
        {
            var homeRoot = Path.Combine(root, "home");
            var workspaceRoot = Path.Combine(root, "workspace");
            Directory.CreateDirectory(homeRoot);
            Directory.CreateDirectory(workspaceRoot);

            Environment.SetEnvironmentVariable("QWEN_CODE_IDE_SERVER_PORT", "4123");
            Environment.SetEnvironmentVariable("QWEN_CODE_IDE_WORKSPACE_PATH", workspaceRoot);
            Environment.SetEnvironmentVariable("QWEN_CODE_IDE_AUTH_TOKEN", "env-secret");

            var backend = new IdeBackendService(
                new FakeDesktopEnvironmentPaths(homeRoot, null, workspaceRoot, workspaceRoot),
                new IdeDetectionService(),
                new IdeContextService(),
                new IdeInstallerService(new FakeIdeCommandRunner(), new FakeDesktopEnvironmentPaths(homeRoot, null, workspaceRoot, workspaceRoot)),
                new FakeIdeProcessProbe(true));

            var snapshot = backend.Inspect(workspaceRoot, "code");

            Assert.Equal("connected", snapshot.Status);
            Assert.Equal("4123", snapshot.Port);
            Assert.Equal("***", snapshot.AuthToken);
            Assert.Equal(workspaceRoot, snapshot.WorkspacePath);
        }
        finally
        {
            Environment.SetEnvironmentVariable("QWEN_CODE_IDE_SERVER_PORT", previousPort);
            Environment.SetEnvironmentVariable("QWEN_CODE_IDE_WORKSPACE_PATH", previousWorkspace);
            Environment.SetEnvironmentVariable("QWEN_CODE_IDE_AUTH_TOKEN", previousAuth);
            Directory.Delete(root, recursive: true);
        }
    }

    private sealed class FakeIdeProcessProbe(bool exists) : IIdeProcessProbe
    {
        public bool Exists(int processId) => exists;
    }

    private sealed class FakeIdeCommandRunner : IIdeCommandRunner
    {
        public List<(string FileName, IReadOnlyList<string> Arguments)> Invocations { get; } = [];

        public Task<IdeCommandResult> RunAsync(
            string fileName,
            IReadOnlyList<string> arguments,
            bool useShellExecute = false,
            CancellationToken cancellationToken = default)
        {
            Invocations.Add((fileName, arguments));
            return Task.FromResult(new IdeCommandResult
            {
                ExitCode = 0,
                StandardOutput = fileName.EndsWith("where.exe", StringComparison.OrdinalIgnoreCase)
                    ? string.Empty
                    : string.Empty,
                StandardError = string.Empty
            });
        }
    }
}
