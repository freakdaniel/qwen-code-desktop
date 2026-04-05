using QwenCode.App.Models;

namespace QwenCode.App.Channels;

/// <summary>
/// Defines the contract for Channel Session Router
/// </summary>
public interface IChannelSessionRouter
{
    /// <summary>
    /// Resolves async
    /// </summary>
    /// <param name="channelName">The channel name</param>
    /// <param name="sessionScope">The session scope</param>
    /// <param name="senderId">The sender id</param>
    /// <param name="chatId">The chat id</param>
    /// <param name="threadId">The thread id</param>
    /// <param name="replyAddress">The reply address</param>
    /// <param name="workingDirectory">The working directory</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that resolves to channel session route</returns>
    Task<ChannelSessionRoute> ResolveAsync(
        string channelName,
        string sessionScope,
        string senderId,
        string chatId,
        string threadId,
        string replyAddress,
        string workingDirectory,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes has session
    /// </summary>
    /// <param name="channelName">The channel name</param>
    /// <param name="senderId">The sender id</param>
    /// <param name="chatId">The chat id</param>
    /// <returns>A value indicating whether the operation succeeded</returns>
    bool HasSession(string channelName, string senderId, string chatId = "");

    /// <summary>
    /// Removes sessions
    /// </summary>
    /// <param name="channelName">The channel name</param>
    /// <param name="senderId">The sender id</param>
    /// <param name="chatId">The chat id</param>
    /// <returns>The resulting i read only list string</returns>
    IReadOnlyList<string> RemoveSessions(string channelName, string senderId, string chatId = "");

    /// <summary>
    /// Lists routes
    /// </summary>
    /// <returns>The resulting i read only list channel session route</returns>
    IReadOnlyList<ChannelSessionRoute> ListRoutes();

    /// <summary>
    /// Executes clear
    /// </summary>
    void Clear();
}
