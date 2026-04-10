using QwenCode.Core.Models;

namespace QwenCode.App.Options;

/// <summary>
/// Represents the Desktop Shell Options
/// </summary>
public sealed class DesktopShellOptions
{
    /// <summary>
    /// Represents the Section Name
    /// </summary>
    public const string SectionName = "DesktopShell";

    /// <summary>
    /// Gets or sets the product name
    /// </summary>
    public string ProductName { get; set; } = "Qwen Code Desktop";

    /// <summary>
    /// Gets or sets the default locale
    /// </summary>
    public string DefaultLocale { get; set; } = "en";

    /// <summary>
    /// Gets or sets the workspace
    /// </summary>
    public WorkspacePaths Workspace { get; set; } = new();
}
