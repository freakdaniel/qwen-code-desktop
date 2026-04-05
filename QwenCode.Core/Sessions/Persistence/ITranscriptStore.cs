using QwenCode.App.Models;

namespace QwenCode.App.Sessions;

public interface ITranscriptStore
{
    IReadOnlyList<SessionPreview> ListSessions(WorkspacePaths paths, int limit = 24);

    DesktopSessionDetail? GetSession(WorkspacePaths paths, GetDesktopSessionRequest request);
}
