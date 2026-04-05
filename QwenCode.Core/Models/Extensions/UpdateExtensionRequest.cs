namespace QwenCode.App.Models;

public sealed class UpdateExtensionRequest
{
    public string Name { get; init; } = string.Empty;

    public bool UpdateAll { get; init; }
}
