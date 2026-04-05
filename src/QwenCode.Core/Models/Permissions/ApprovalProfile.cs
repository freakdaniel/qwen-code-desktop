namespace QwenCode.App.Models;

/// <summary>
/// Represents the Approval Profile
/// </summary>
public sealed class ApprovalProfile
{
    /// <summary>
    /// Gets or sets the default mode
    /// </summary>
    public required string DefaultMode { get; init; }

    /// <summary>
    /// Gets or sets the confirm shell commands
    /// </summary>
    public bool? ConfirmShellCommands { get; init; }

    /// <summary>
    /// Gets or sets the confirm file edits
    /// </summary>
    public bool? ConfirmFileEdits { get; init; }

    /// <summary>
    /// Gets or sets the allow rules
    /// </summary>
    public required IReadOnlyList<string> AllowRules { get; init; }

    /// <summary>
    /// Gets or sets the ask rules
    /// </summary>
    public required IReadOnlyList<string> AskRules { get; init; }

    /// <summary>
    /// Gets or sets the deny rules
    /// </summary>
    public required IReadOnlyList<string> DenyRules { get; init; }
}
