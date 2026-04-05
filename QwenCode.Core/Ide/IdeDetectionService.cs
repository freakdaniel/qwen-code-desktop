using QwenCode.App.Models;

namespace QwenCode.App.Ide;

public sealed class IdeDetectionService : IIdeDetectionService
{
    private static readonly IdeInfo Devin = new() { Name = "devin", DisplayName = "Devin" };
    private static readonly IdeInfo Replit = new() { Name = "replit", DisplayName = "Replit" };
    private static readonly IdeInfo Cursor = new() { Name = "cursor", DisplayName = "Cursor" };
    private static readonly IdeInfo CloudShell = new() { Name = "cloudshell", DisplayName = "Cloud Shell" };
    private static readonly IdeInfo Codespaces = new() { Name = "codespaces", DisplayName = "GitHub Codespaces" };
    private static readonly IdeInfo FirebaseStudio = new() { Name = "firebasestudio", DisplayName = "Firebase Studio" };
    private static readonly IdeInfo Trae = new() { Name = "trae", DisplayName = "Trae" };
    private static readonly IdeInfo Vscode = new() { Name = "vscode", DisplayName = "VS Code" };
    private static readonly IdeInfo VscodeFork = new() { Name = "vscodefork", DisplayName = "IDE" };

    public IdeInfo? Detect(string processCommand, IReadOnlyDictionary<string, string>? environment = null, IdeInfo? overrideInfo = null)
    {
        if (!string.IsNullOrWhiteSpace(overrideInfo?.Name) && !string.IsNullOrWhiteSpace(overrideInfo.DisplayName))
        {
            return overrideInfo;
        }

        environment ??= Environment.GetEnvironmentVariables()
            .Cast<System.Collections.DictionaryEntry>()
            .Where(static entry => entry.Key is string && entry.Value is not null)
            .ToDictionary(
                static entry => (string)entry.Key,
                static entry => entry.Value?.ToString() ?? string.Empty,
                StringComparer.OrdinalIgnoreCase);

        if (!environment.TryGetValue("TERM_PROGRAM", out var termProgram) ||
            !string.Equals(termProgram, "vscode", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (environment.ContainsKey("__COG_BASHRC_SOURCED"))
        {
            return Devin;
        }

        if (environment.ContainsKey("REPLIT_USER"))
        {
            return Replit;
        }

        if (environment.ContainsKey("CURSOR_TRACE_ID"))
        {
            return Cursor;
        }

        if (environment.ContainsKey("CODESPACES"))
        {
            return Codespaces;
        }

        if (environment.ContainsKey("EDITOR_IN_CLOUD_SHELL") || environment.ContainsKey("CLOUD_SHELL"))
        {
            return CloudShell;
        }

        if (environment.TryGetValue("TERM_PRODUCT", out var termProduct) &&
            string.Equals(termProduct, "Trae", StringComparison.OrdinalIgnoreCase))
        {
            return Trae;
        }

        if (environment.ContainsKey("MONOSPACE_ENV"))
        {
            return FirebaseStudio;
        }

        return processCommand.Contains("code", StringComparison.OrdinalIgnoreCase) ? Vscode : VscodeFork;
    }
}
