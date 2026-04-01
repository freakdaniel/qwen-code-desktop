using QwenCode.App.Models;

namespace QwenCode.App.Sessions;

public interface ITranscriptStore
{
    IReadOnlyList<SessionPreview> ListSessions(SourceMirrorPaths paths, int limit = 24);

    DesktopSessionDetail? GetSession(SourceMirrorPaths paths, string sessionId);
}
