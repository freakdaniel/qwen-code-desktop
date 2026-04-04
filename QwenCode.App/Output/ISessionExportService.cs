using QwenCode.App.Models;

namespace QwenCode.App.Output;

public interface ISessionExportService
{
    SessionExportSnapshot? BuildSessionSnapshot(WorkspacePaths paths, GetDesktopSessionRequest request);

    string FormatSession(WorkspacePaths paths, GetDesktopSessionRequest request, OutputFormat format);
}
