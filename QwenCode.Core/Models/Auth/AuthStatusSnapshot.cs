namespace QwenCode.App.Models;

public sealed class AuthStatusSnapshot
{
    public required string SelectedType { get; init; }

    public required string SelectedScope { get; init; }

    public required string DisplayName { get; init; }

    public required string Status { get; init; }

    public required string Model { get; init; }

    public required string Endpoint { get; init; }

    public required string ApiKeyEnvironmentVariable { get; init; }

    public required bool HasApiKey { get; init; }

    public required bool HasQwenOAuthCredentials { get; init; }

    public required bool HasRefreshToken { get; init; }

    public required string CredentialPath { get; init; }

    public required string LastError { get; init; }

    public DateTimeOffset? LastAuthenticatedAtUtc { get; init; }

    public QwenOAuthDeviceFlowSnapshot? DeviceFlow { get; init; }
}
