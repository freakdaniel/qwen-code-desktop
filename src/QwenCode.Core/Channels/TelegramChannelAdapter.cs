using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using QwenCode.Core.Models;

namespace QwenCode.Core.Channels;

/// <summary>
/// Represents the Telegram Channel Adapter
/// </summary>
/// <param name="httpClient">The http client</param>
public sealed class TelegramChannelAdapter(HttpClient httpClient) : ChannelAdapterBase("telegram")
{
    /// <summary>
    /// Normalizes inbound
    /// </summary>
    /// <param name="channelName">The channel name</param>
    /// <param name="payload">The payload</param>
    /// <returns>The resulting channel envelope</returns>
    public override ChannelEnvelope NormalizeInbound(string channelName, JsonElement payload) =>
        new()
        {
            ChannelName = channelName,
            SenderId = GetRequiredString(payload, "senderId"),
            SenderName = GetOptionalString(payload, "senderName"),
            ChatId = GetRequiredString(payload, "chatId"),
            Text = GetRequiredString(payload, "text"),
            ThreadId = GetOptionalString(payload, "threadId"),
            IsGroup = GetOptionalBoolean(payload, "isGroup"),
            IsMentioned = GetOptionalBoolean(payload, "isMentioned"),
            IsReplyToBot = GetOptionalBoolean(payload, "isReplyToBot"),
            ReferencedText = GetOptionalString(payload, "referencedText"),
            ImageBase64 = GetOptionalString(payload, "imageBase64"),
            ImageMimeType = GetOptionalString(payload, "imageMimeType"),
            Attachments = GetOptionalAttachments(payload, "attachments")
        };

    /// <summary>
    /// Executes send outbound async
    /// </summary>
    /// <param name="configuration">The configuration to apply</param>
    /// <param name="route">The route</param>
    /// <param name="message">The message</param>
    /// <param name="payload">The payload</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that resolves to bool</returns>
    public override async Task<bool> SendOutboundAsync(
        ChannelRuntimeConfiguration configuration,
        ChannelSessionRoute route,
        ChannelOutboundMessage message,
        JsonObject payload,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(configuration.Token) ||
            string.IsNullOrWhiteSpace(route.ChatId) ||
            string.IsNullOrWhiteSpace(message.Text) ||
            string.Equals(message.Kind, "chunk", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var endpoint = $"https://api.telegram.org/bot{configuration.Token}/sendMessage";
        using var response = await httpClient.PostAsJsonAsync(
            endpoint,
            new
            {
                chat_id = route.ChatId,
                text = message.Text
            },
            cancellationToken);

        return response.IsSuccessStatusCode;
    }
}
