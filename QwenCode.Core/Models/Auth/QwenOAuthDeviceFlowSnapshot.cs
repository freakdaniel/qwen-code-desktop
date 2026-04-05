namespace QwenCode.App.Models;

public sealed class QwenOAuthDeviceFlowSnapshot
{
    public required string FlowId { get; init; }

    public required string Scope { get; init; }

    public required string Status { get; init; }

    public required string VerificationUri { get; init; }

    public required string VerificationUriComplete { get; init; }

    public required string UserCode { get; init; }

    public required int PollIntervalMs { get; init; }

    public required bool BrowserLaunchAttempted { get; init; }

    public required bool BrowserLaunchSucceeded { get; init; }

    public required DateTimeOffset StartedAtUtc { get; init; }

    public required DateTimeOffset ExpiresAtUtc { get; init; }

    public DateTimeOffset? CompletedAtUtc { get; init; }

    public string ErrorMessage { get; init; } = string.Empty;
}
