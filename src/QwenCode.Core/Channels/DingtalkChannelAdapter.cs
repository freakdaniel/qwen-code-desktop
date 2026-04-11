using QwenCode.Core.Models;

namespace QwenCode.Core.Channels;

/// <summary>
/// Represents the Dingtalk Channel Adapter
/// </summary>
/// <param name="httpClient">The http client</param>
public sealed class DingtalkChannelAdapter(HttpClient httpClient) : ChannelAdapterBase("dingtalk")
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
            ReplyAddress = GetOptionalString(payload, "sessionWebhook"),
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
        if (string.IsNullOrWhiteSpace(route.ReplyAddress) ||
            string.IsNullOrWhiteSpace(message.Text) ||
            string.Equals(message.Kind, "chunk", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var chunks = SplitDingtalkMarkdown(message.Text);
        foreach (var chunk in chunks)
        {
            using var response = await httpClient.PostAsJsonAsync(
                route.ReplyAddress,
                new
                {
                    msgtype = "markdown",
                    markdown = new
                    {
                        title = ExtractTitle(message.Text),
                        text = chunk
                    }
                },
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return false;
            }
        }

        return true;
    }

    private static IReadOnlyList<string> SplitDingtalkMarkdown(string text)
    {
        const int chunkLimit = 3800;
        if (string.IsNullOrWhiteSpace(text) || text.Length <= chunkLimit)
        {
            return [text];
        }

        var chunks = new List<string>();
        var lines = text.Split('\n');
        var buffer = new System.Text.StringBuilder();

        foreach (var line in lines)
        {
            var candidateLength = buffer.Length == 0 ? line.Length : buffer.Length + 1 + line.Length;
            if (candidateLength > chunkLimit && buffer.Length > 0)
            {
                chunks.Add(buffer.ToString());
                buffer.Clear();
            }

            if (buffer.Length > 0)
            {
                buffer.AppendLine();
            }

            buffer.Append(line);
        }

        if (buffer.Length > 0)
        {
            chunks.Add(buffer.ToString());
        }

        return chunks;
    }

    private static string ExtractTitle(string text)
    {
        var firstLine = text.Split('\n')[0];
        if (string.IsNullOrWhiteSpace(firstLine))
        {
            return "Reply";
        }

        return firstLine.TrimStart('#', '*', '-', ' ', '>')[..Math.Min(20, firstLine.TrimStart('#', '*', '-', ' ', '>').Length)];
    }
}
