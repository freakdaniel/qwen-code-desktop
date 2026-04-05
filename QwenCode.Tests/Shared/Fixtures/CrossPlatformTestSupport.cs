namespace QwenCode.Tests.Shared.Fixtures;

internal static class CrossPlatformTestSupport
{
    public static string CreateHookCommand(
        string root,
        string baseName,
        string windowsScript,
        string unixScript)
    {
        var scriptPath = CreateExecutableScript(root, baseName, windowsScript, unixScript);
        return OperatingSystem.IsWindows()
            ? $"& '{scriptPath.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("'", "''", StringComparison.Ordinal)}'"
            : $"'{scriptPath.Replace("'", "'\\''", StringComparison.Ordinal)}'";
    }

    public static string CreateExecutableScript(
        string root,
        string baseName,
        string windowsScript,
        string unixScript)
    {
        Directory.CreateDirectory(root);

        if (OperatingSystem.IsWindows())
        {
            var windowsPath = Path.Combine(root, $"{baseName}.ps1");
            File.WriteAllText(windowsPath, windowsScript);
            return windowsPath;
        }

        var unixPath = Path.Combine(root, $"{baseName}.sh");
        var content = unixScript.StartsWith("#!", StringComparison.Ordinal)
            ? unixScript
            : "#!/bin/sh" + Environment.NewLine + unixScript;
        File.WriteAllText(unixPath, content);
        File.SetUnixFileMode(
            unixPath,
            UnixFileMode.UserRead |
            UnixFileMode.UserWrite |
            UnixFileMode.UserExecute |
            UnixFileMode.GroupRead |
            UnixFileMode.GroupExecute |
            UnixFileMode.OtherRead |
            UnixFileMode.OtherExecute);
        return unixPath;
    }

    public static string ResolveUpstreamRepoRoot()
    {
        var configured = Environment.GetEnvironmentVariable("QWEN_CODE_MAIN_ROOT");
        if (!string.IsNullOrWhiteSpace(configured) && Directory.Exists(configured))
        {
            return configured;
        }

        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var solutionMarker = Path.Combine(current.FullName, "QwenCode.slnx");
            if (File.Exists(solutionMarker))
            {
                var sibling = Path.GetFullPath(Path.Combine(current.FullName, "..", "qwen-code-main"));
                if (Directory.Exists(sibling))
                {
                    return sibling;
                }

                break;
            }

            current = current.Parent;
        }

        return string.Empty;
    }

    public static string GetReadFileShellCommand(string relativePath) =>
        OperatingSystem.IsWindows()
            ? $"type {relativePath.Replace('/', '\\')}"
            : $"cat {relativePath.Replace('\\', '/')}";

    public static string GetWriteFileShellCommand(string relativePath, string content) =>
        OperatingSystem.IsWindows()
            ? $"echo {content} > {relativePath.Replace('/', '\\')}"
            : $"echo {content} > {relativePath.Replace('\\', '/')}";
}
