using System.Runtime.InteropServices;
using QwenCode.App.Infrastructure;
using QwenCode.App.Models;

namespace QwenCode.App.Ide;

/// <summary>
/// Represents the Ide Installer Service
/// </summary>
/// <param name="commandRunner">The command runner</param>
/// <param name="environmentPaths">The environment paths</param>
public sealed class IdeInstallerService(
    IIdeCommandRunner commandRunner,
    IDesktopEnvironmentPaths environmentPaths) : IIdeInstallerService
{
    private const string CompanionExtensionId = "qwenlm.qwen-code-vscode-ide-companion";

    /// <summary>
    /// Executes install companion async
    /// </summary>
    /// <param name="ide">The ide</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that resolves to ide install result</returns>
    public async Task<IdeInstallResult> InstallCompanionAsync(IdeInfo ide, CancellationToken cancellationToken = default)
    {
        var commandPath = await FindVsCodeCommandAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(commandPath))
        {
            return new IdeInstallResult
            {
                Success = false,
                Message = $"{ide.DisplayName} CLI not found. Install the companion extension manually or make the code CLI available in PATH."
            };
        }

        var result = await commandRunner.RunAsync(
            commandPath,
            ["--install-extension", CompanionExtensionId, "--force"],
            useShellExecute: false,
            cancellationToken);

        return new IdeInstallResult
        {
            Success = result.Success,
            CommandPath = commandPath,
            Message = result.Success
                ? $"{ide.DisplayName} companion extension was installed successfully."
                : $"Failed to install the {ide.DisplayName} companion extension. {result.StandardError}".Trim()
        };
    }

    private async Task<string> FindVsCodeCommandAsync(CancellationToken cancellationToken)
    {
        var candidates = GetCandidatePaths().ToArray();
        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        var commandName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "code.cmd" : "code";
        try
        {
            var probe = await commandRunner.RunAsync(
                RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "where.exe" : "where",
                [commandName],
                cancellationToken: cancellationToken);
            if (probe.Success)
            {
                return probe.StandardOutput
                    .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                    .FirstOrDefault() ?? commandName;
            }
        }
        catch
        {
            // Fall back to the command name.
        }

        return string.Empty;
    }

    private IEnumerable<string> GetCandidatePaths()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            yield return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "Microsoft VS Code",
                "bin",
                "code.cmd");
            yield return Path.Combine(
                environmentPaths.HomeDirectory,
                "AppData",
                "Local",
                "Programs",
                "Microsoft VS Code",
                "bin",
                "code.cmd");
            yield break;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            yield return "/Applications/Visual Studio Code.app/Contents/Resources/app/bin/code";
            yield return Path.Combine(environmentPaths.HomeDirectory, "Library", "Application Support", "Code", "bin", "code");
            yield break;
        }

        yield return "/usr/share/code/bin/code";
        yield return "/snap/bin/code";
        yield return Path.Combine(environmentPaths.HomeDirectory, ".local", "share", "code", "bin", "code");
    }
}
