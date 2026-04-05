namespace QwenCode.App.Models;

public sealed class CommandHookConfiguration
{
    public required string Command { get; init; }

    public string Name { get; init; } = string.Empty;

    public string Matcher { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public int TimeoutMs { get; init; } = 60_000;

    public IReadOnlyDictionary<string, string> EnvironmentVariables { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public HookConfigSource Source { get; init; }

    public HookEventName EventName { get; init; } = HookEventName.UserPromptSubmit;

    public bool Sequential { get; init; }
}
