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

        Process[] processes;
        try
        {
            processes = Process.GetProcessesByName("electron");
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Unable to enumerate Electron processes");
            return;
        }

        foreach (var process in processes)
        {
            try
            {
                // Skip if process is already exiting to avoid Win32Exception (299)
                // when accessing MainModule of a dying process
                if (process.HasExited)
                {
                    continue;
                }

                // Add small delay to let process stabilize if it's in shutdown transition
                try
                {
                    process.WaitForExit(100);
                }
                catch
                {
                    // Process might exit during wait, which is fine
                }

                if (process.HasExited)
                {
                    continue;
                }

                string? processPath = null;
                try
                {
                    processPath = process.MainModule?.FileName;
                }
                catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 299 || ex.NativeErrorCode == 5)
                {
                    // 299 = "Only part of a ReadProcessMemory or WriteProcessMemory request was completed"
                    // 5 = "Access denied" (process is exiting)
                    logger.LogDebug(
                        "Cannot inspect Electron process {ProcessId} - it is likely shutting down",
                        process.Id);
                    continue;
                }

                if (!string.Equals(processPath, expectedElectronPath, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                logger.LogWarning(
                    "Stopping Electron host process {ProcessId} from {ProcessPath} ({Reason}).",
                    process.Id,
                    processPath,
                    reason);

                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (System.ComponentModel.Win32Exception)
                {
                    // Process already exiting, which is fine
                    logger.LogDebug("Electron process {ProcessId} already exiting", process.Id);
                }

                process.WaitForExit(5000);
            }
            catch (Exception exception) when (exception is not System.ComponentModel.Win32Exception)
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
