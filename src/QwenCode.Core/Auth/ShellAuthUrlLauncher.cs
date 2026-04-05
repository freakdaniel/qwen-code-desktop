using System.Diagnostics;

namespace QwenCode.App.Auth;

/// <summary>
/// Represents the Shell Auth Url Launcher
/// </summary>
public sealed class ShellAuthUrlLauncher : IAuthUrlLauncher
{
    /// <summary>
    /// Executes launch async
    /// </summary>
    /// <param name="url">The url</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that resolves to bool</returns>
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
