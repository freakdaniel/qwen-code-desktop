namespace QwenCode.App.Models;

public sealed class IdeContextSnapshot
{
    public IReadOnlyList<IdeOpenFile> OpenFiles { get; init; } = [];

    public bool? IsTrusted { get; init; }
}
