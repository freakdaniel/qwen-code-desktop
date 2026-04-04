using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using QwenCode.App.Infrastructure;
using QwenCode.App.Models;

namespace QwenCode.App.Channels;

public sealed class WeixinChannelAdapter(
    HttpClient httpClient,
    IDesktopEnvironmentPaths environmentPaths) : ChannelAdapterBase("weixin")
{
    public override ChannelEnvelope NormalizeInbound(string channelName, JsonElement payload) =>
        new()
        {
            ChannelName = channelName,
            SenderId = GetRequiredString(payload, "senderId"),
            SenderName = GetOptionalString(payload, "senderName"),
            ChatId = string.IsNullOrWhiteSpace(GetOptionalString(payload, "chatId"))
                ? GetRequiredString(payload, "senderId")
                : GetOptionalString(payload, "chatId"),
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
        if (string.IsNullOrWhiteSpace(route.ChatId) ||
            string.IsNullOrWhiteSpace(message.Text) ||
            string.Equals(message.Kind, "chunk", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var account = LoadWeixinAccount();
        var token = !string.IsNullOrWhiteSpace(configuration.Token) ? configuration.Token : account.Token;
        var baseUrl = !string.IsNullOrWhiteSpace(configuration.BaseUrl) ? configuration.BaseUrl : account.BaseUrl;
        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(baseUrl))
        {
            return false;
        }

        var endpoint = $"{baseUrl.TrimEnd('/')}/v2/message/send";
        using var response = await httpClient.PostAsJsonAsync(
            endpoint,
            new
            {
                to_user_id = route.ChatId,
                from_user_id = string.Empty,
                client_id = Guid.NewGuid().ToString("N"),
                message_type = "bot",
                message_state = "finish",
                context_token = string.Empty,
                item_list = new object[]
                {
                    new
                    {
                        type = "text",
                        text_item = new
                        {
                            text = MarkdownToPlainText(message.Text)
                        }
                    }
                }
            },
            cancellationToken);

        return response.IsSuccessStatusCode;
    }

    private (string Token, string BaseUrl) LoadWeixinAccount()
    {
        var path = Path.Combine(environmentPaths.HomeDirectory, ".qwen", "channels", "weixin", "account.json");
        if (!File.Exists(path))
        {
            return (string.Empty, string.Empty);
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var root = document.RootElement;
            var token = root.TryGetProperty("token", out var tokenProperty) && tokenProperty.ValueKind == JsonValueKind.String
                ? tokenProperty.GetString() ?? string.Empty
                : string.Empty;
            var baseUrl = root.TryGetProperty("baseUrl", out var baseUrlProperty) && baseUrlProperty.ValueKind == JsonValueKind.String
                ? baseUrlProperty.GetString() ?? string.Empty
                : "https://ilinkai.weixin.qq.com";
            return (token, baseUrl);
        }
        catch
        {
            return (string.Empty, string.Empty);
        }
    }

    private static string MarkdownToPlainText(string text) =>
        text
            .Replace("```", string.Empty, StringComparison.Ordinal)
            .Replace("**", string.Empty, StringComparison.Ordinal)
            .Replace("__", string.Empty, StringComparison.Ordinal)
            .Replace("`", string.Empty, StringComparison.Ordinal)
            .Replace("~~", string.Empty, StringComparison.Ordinal)
            .Trim();
}
