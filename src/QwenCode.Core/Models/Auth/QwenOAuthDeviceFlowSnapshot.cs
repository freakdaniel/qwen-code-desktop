namespace QwenCode.App.Models;

/// <summary>
/// Represents the Qwen O Auth Device Flow Snapshot
/// </summary>
public sealed class QwenOAuthDeviceFlowSnapshot
{
    /// <summary>
    /// Gets or sets the flow id
    /// </summary>
    public required string FlowId { get; init; }

    /// <summary>
    /// Gets or sets the scope
    /// </summary>
    public required string Scope { get; init; }

    /// <summary>
    /// Gets or sets the status
    /// </summary>
    public required string Status { get; init; }

    /// <summary>
    /// Gets or sets the verification uri
    /// </summary>
    public required string VerificationUri { get; init; }

    /// <summary>
    /// Gets or sets the verification uri complete
    /// </summary>
    public required string VerificationUriComplete { get; init; }

    /// <summary>
    /// Gets or sets the user code
    /// </summary>
    public required string UserCode { get; init; }

    /// <summary>
    /// Gets or sets the poll interval ms
    /// </summary>
    public required int PollIntervalMs { get; init; }

    /// <summary>
    /// Gets or sets the browser launch attempted
    /// </summary>
    public required bool BrowserLaunchAttempted { get; init; }

    /// <summary>
    /// Gets or sets the browser launch succeeded
    /// </summary>
    public required bool BrowserLaunchSucceeded { get; init; }

    /// <summary>
    /// Gets or sets the started at utc
    /// </summary>
    public required DateTimeOffset StartedAtUtc { get; init; }

    /// <summary>
    /// Gets or sets the expires at utc
    /// </summary>
    public required DateTimeOffset ExpiresAtUtc { get; init; }

    /// <summary>
    /// Gets or sets the completed at utc
    /// </summary>
    public DateTimeOffset? CompletedAtUtc { get; init; }

    /// <summary>
    /// Gets or sets the error message
    /// </summary>
    public string ErrorMessage { get; init; } = string.Empty;
}
