namespace QwenCode.App.Models;

public sealed class DisconnectAuthRequest
{
    public required string Scope { get; init; }

    public bool ClearPersistedCredentials { get; init; } = true;
}
