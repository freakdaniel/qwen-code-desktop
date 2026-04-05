namespace QwenCode.App.Models;

/// <summary>
/// Represents the Auth Status Snapshot
/// </summary>
public sealed class AuthStatusSnapshot
{
    /// <summary>
    /// Gets or sets the selected type
    /// </summary>
    public required string SelectedType { get; init; }

    /// <summary>
    /// Gets or sets the selected scope
    /// </summary>
    public required string SelectedScope { get; init; }

    /// <summary>
    /// Gets or sets the display name
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Gets or sets the status
    /// </summary>
    public required string Status { get; init; }

    /// <summary>
    /// Gets or sets the model
    /// </summary>
    public required string Model { get; init; }

    /// <summary>
    /// Gets or sets the endpoint
    /// </summary>
    public required string Endpoint { get; init; }

    /// <summary>
    /// Gets or sets the api key environment variable
    /// </summary>
    public required string ApiKeyEnvironmentVariable { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether has api key
    /// </summary>
    public required bool HasApiKey { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether has qwen o auth credentials
    /// </summary>
    public required bool HasQwenOAuthCredentials { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether has refresh token
    /// </summary>
    public required bool HasRefreshToken { get; init; }

    /// <summary>
    /// Gets or sets the credential path
    /// </summary>
    public required string CredentialPath { get; init; }

    /// <summary>
    /// Gets or sets the last error
    /// </summary>
    public required string LastError { get; init; }

    /// <summary>
    /// Gets or sets the last authenticated at utc
    /// </summary>
    public DateTimeOffset? LastAuthenticatedAtUtc { get; init; }

    /// <summary>
    /// Gets or sets the device flow
    /// </summary>
    public QwenOAuthDeviceFlowSnapshot? DeviceFlow { get; init; }
}
