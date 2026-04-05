namespace QwenCode.App.Models;

/// <summary>
/// Represents the Command Hook Configuration
/// </summary>
public sealed class CommandHookConfiguration
{
    /// <summary>
    /// Gets or sets the command
    /// </summary>
    public required string Command { get; init; }

    /// <summary>
    /// Gets or sets the name
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the matcher
    /// </summary>
    public string Matcher { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the description
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the timeout ms
    /// </summary>
    public int TimeoutMs { get; init; } = 60_000;

    /// <summary>
    /// Gets or sets the environment variables
    /// </summary>
    public IReadOnlyDictionary<string, string> EnvironmentVariables { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets or sets the source
    /// </summary>
    public HookConfigSource Source { get; init; }

    /// <summary>
    /// Gets or sets the event name
    /// </summary>
    public HookEventName EventName { get; init; } = HookEventName.UserPromptSubmit;

    /// <summary>
    /// Gets or sets the sequential
    /// </summary>
    public bool Sequential { get; init; }
}
