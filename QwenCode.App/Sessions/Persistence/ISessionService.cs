using QwenCode.App.Models;

namespace QwenCode.App.Sessions;

public interface ISessionService
{
    bool SessionExists(WorkspacePaths paths, string sessionId);

    SessionPreview? LoadLastSession(WorkspacePaths paths);

    SessionConversationRecord? LoadConversation(WorkspacePaths paths, string sessionId);

    bool RemoveSession(WorkspacePaths paths, string sessionId);
}
