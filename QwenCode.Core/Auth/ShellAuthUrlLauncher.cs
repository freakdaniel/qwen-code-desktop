using System.Diagnostics;

namespace QwenCode.App.Auth;

public sealed class ShellAuthUrlLauncher : IAuthUrlLauncher
{
    public Task<bool> LaunchAsync(string url, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return Task.FromResult(false);
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
            return Task.FromResult(true);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }
}
