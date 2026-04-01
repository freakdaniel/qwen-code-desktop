namespace QwenCode.App.Models;

public sealed class QwenApprovalProfile
{
    public required string DefaultMode { get; init; }

    public bool? ConfirmShellCommands { get; init; }

    public bool? ConfirmFileEdits { get; init; }

    public required IReadOnlyList<string> AllowRules { get; init; }

    public required IReadOnlyList<string> AskRules { get; init; }

    public required IReadOnlyList<string> DenyRules { get; init; }
}
