namespace QwenCode.App.Auth;

public interface IAuthUrlLauncher
{
    Task<bool> LaunchAsync(string url, CancellationToken cancellationToken = default);
}
