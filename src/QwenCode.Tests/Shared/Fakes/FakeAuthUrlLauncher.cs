using QwenCode.App.Auth;

namespace QwenCode.Tests.Shared.Fakes;

internal sealed class FakeAuthUrlLauncher(bool launchResult = true) : IAuthUrlLauncher
{
    public string LastUrl { get; private set; } = string.Empty;

    public int LaunchCount { get; private set; }

    public Task<bool> LaunchAsync(string url, CancellationToken cancellationToken = default)
    {
        LastUrl = url;
        LaunchCount++;
        return Task.FromResult(launchResult);
    }
}
