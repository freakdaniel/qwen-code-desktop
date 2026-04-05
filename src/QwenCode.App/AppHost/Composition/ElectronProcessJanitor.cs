using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace QwenCode.App.AppHost;

internal static class ElectronProcessJanitor
{
    /// <summary>
    /// Cleans up stale unpacked hosts
    /// </summary>
    /// <param name="logger">The logger</param>
    public static void CleanupStaleUnpackedHosts(ILogger logger)
    {
        CleanupMatchingUnpackedHosts(logger, "before startup");
    }

    /// <summary>
    /// Cleans up current unpacked host
    /// </summary>
    /// <param name="logger">The logger</param>
    /// <param name="reason">The reason</param>
    public static void CleanupCurrentUnpackedHost(ILogger logger, string reason)
    {
        CleanupMatchingUnpackedHosts(logger, reason);
    }

    private static void CleanupMatchingUnpackedHosts(ILogger logger, string reason)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var expectedElectronPath = Path.Combine(
            AppContext.BaseDirectory,
            ".electron",
            "node_modules",
            "electron",
            "dist",
            "electron.exe");

        if (!File.Exists(expectedElectronPath))
        {
            return;
        }

        foreach (var process in Process.GetProcessesByName("electron"))
        {
            try
            {
                var processPath = process.MainModule?.FileName;
                if (!string.Equals(processPath, expectedElectronPath, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                logger.LogWarning(
                    "Stopping Electron host process {ProcessId} from {ProcessPath} ({Reason}).",
                    process.Id,
                    processPath,
                    reason);

                process.Kill(entireProcessTree: true);
                process.WaitForExit(5000);
            }
            catch (Exception exception)
            {
                logger.LogWarning(
                    exception,
                    "Failed to inspect or stop stale Electron host process {ProcessId}.",
                    process.Id);
            }
            finally
            {
                process.Dispose();
            }
        }
    }
}
