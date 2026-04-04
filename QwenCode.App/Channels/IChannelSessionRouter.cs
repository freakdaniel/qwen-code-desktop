using QwenCode.App.Models;

namespace QwenCode.App.Channels;

public interface IChannelSessionRouter
{
    Task<ChannelSessionRoute> ResolveAsync(
        string channelName,
        string sessionScope,
        string senderId,
        string chatId,
        string threadId,
        string replyAddress,
        string workingDirectory,
        CancellationToken cancellationToken = default);

    bool HasSession(string channelName, string senderId, string chatId = "");

    IReadOnlyList<string> RemoveSessions(string channelName, string senderId, string chatId = "");

    IReadOnlyList<ChannelSessionRoute> ListRoutes();

    void Clear();
}
