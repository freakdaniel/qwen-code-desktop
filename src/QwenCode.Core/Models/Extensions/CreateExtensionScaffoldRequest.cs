namespace QwenCode.App.Models;

/// <summary>
/// Represents the Create Extension Scaffold Request
/// </summary>
public sealed class CreateExtensionScaffoldRequest
{
    /// <summary>
    /// Gets or sets the target path
    /// </summary>
    public required string TargetPath { get; init; }

    /// <summary>
    /// Gets or sets the template
    /// </summary>
    public string Template { get; init; } = string.Empty;
}
