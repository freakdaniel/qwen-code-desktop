namespace QwenCode.App.Models;

public sealed class GetPromptRegistryRequest
{
    public string ServerName { get; init; } = string.Empty;

    public bool ForceRefresh { get; init; }
}
