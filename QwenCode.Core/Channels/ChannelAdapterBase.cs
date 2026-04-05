using System.Text.Json;
using System.Text.Json.Nodes;
using QwenCode.App.Models;

namespace QwenCode.App.Channels;

public abstract class ChannelAdapterBase(string channelType) : IChannelAdapter
{
    public string ChannelType { get; } = channelType;

    public virtual Task ConnectAsync(ChannelRuntimeConfiguration configuration, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public virtual Task DisconnectAsync(ChannelRuntimeConfiguration configuration, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public abstract ChannelEnvelope NormalizeInbound(string channelName, JsonElement payload);

    public virtual JsonObject CreateOutboundPayload(ChannelSessionRoute route, ChannelOutboundMessage message) =>
        new()
        {
            ["channel"] = route.ChannelName,
            ["chatId"] = route.ChatId,
            ["senderId"] = route.SenderId,
            ["sessionId"] = route.SessionId,
            ["threadId"] = route.ThreadId,
            ["replyAddress"] = route.ReplyAddress,
            ["workingDirectory"] = route.WorkingDirectory,
            ["kind"] = message.Kind,
            ["text"] = message.Text,
            ["toolName"] = message.ToolName,
            ["commandName"] = message.CommandName
        };

    public virtual Task<bool> SendOutboundAsync(
        ChannelRuntimeConfiguration configuration,
        ChannelSessionRoute route,
        ChannelOutboundMessage message,
        JsonObject payload,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(false);

    protected static string GetRequiredString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException($"Channel payload is missing required string property '{propertyName}'.");
        }

        return property.GetString() ?? string.Empty;
    }

    protected static string GetOptionalString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
        {
            return string.Empty;
        }

        return property.GetString() ?? string.Empty;
    }

    protected static bool GetOptionalBoolean(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return false;
        }

        return property.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => false
        };
    }

    protected static IReadOnlyList<ChannelAttachment> GetOptionalAttachments(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var attachments = new List<ChannelAttachment>();
        foreach (var item in property.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            attachments.Add(new ChannelAttachment
            {
                Type = GetOptionalString(item, "type"),
                Data = GetOptionalString(item, "data"),
                FilePath = GetOptionalString(item, "filePath"),
                MimeType = GetOptionalString(item, "mimeType"),
                FileName = GetOptionalString(item, "fileName")
            });
        }

        return attachments;
    }
}
