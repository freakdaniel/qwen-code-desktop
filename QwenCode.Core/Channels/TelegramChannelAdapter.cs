using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using QwenCode.App.Models;

namespace QwenCode.App.Channels;

public sealed class TelegramChannelAdapter(HttpClient httpClient) : ChannelAdapterBase("telegram")
{
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
