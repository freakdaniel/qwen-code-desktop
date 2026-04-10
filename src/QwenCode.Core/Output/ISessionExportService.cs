using QwenCode.Core.Models;

namespace QwenCode.Core.Output;

/// <summary>
/// Defines the contract for Session Export Service
/// </summary>
public interface ISessionExportService
{
    /// <summary>
    /// Builds session snapshot
    /// </summary>
    /// <param name="paths">The paths to process</param>
    /// <param name="request">The request payload</param>
    /// <returns>The resulting session export snapshot?</returns>
    SessionExportSnapshot? BuildSessionSnapshot(WorkspacePaths paths, GetDesktopSessionRequest request);

    /// <summary>
    /// Executes format session
    /// </summary>
    /// <param name="paths">The paths to process</param>
    /// <param name="request">The request payload</param>
    /// <param name="format">The format</param>
    /// <returns>The resulting string</returns>
    string FormatSession(WorkspacePaths paths, GetDesktopSessionRequest request, OutputFormat format);
}
