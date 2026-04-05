namespace QwenCode.App.Models;

/// <summary>
/// Represents the User Prompt Hook Request
/// </summary>
public sealed class UserPromptHookRequest
{
    /// <summary>
    /// Gets or sets the session id
    /// </summary>
    public required string SessionId { get; init; }

    /// <summary>
    /// Gets or sets the prompt
    /// </summary>
    public required string Prompt { get; init; }

    /// <summary>
    /// Gets or sets the working directory
    /// </summary>
    public required string WorkingDirectory { get; init; }

    /// <summary>
    /// Gets or sets the transcript path
    /// </summary>
    public required string TranscriptPath { get; init; }
}
