namespace QwenCode.App.Models;

public sealed class InvokePromptRegistryEntryRequest
{
    public string Name { get; init; } = string.Empty;

    public string ArgumentsJson { get; init; } = "{}";
}
