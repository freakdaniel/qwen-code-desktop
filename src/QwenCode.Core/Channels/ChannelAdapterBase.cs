using System.Text.Json;
using System.Text.Json.Nodes;
using QwenCode.App.Models;

namespace QwenCode.App.Channels;

/// <summary>
/// Provides the base implementation for Channel Adapter Base
/// </summary>
/// <param name="channelType">The channel type</param>
public abstract class ChannelAdapterBase(string channelType) : IChannelAdapter
{
    /// <summary>
    /// Gets the channel type
    /// </summary>
    public string ChannelType { get; } = channelType;

    /// <summary>
    /// Connects async
    /// </summary>
    /// <param name="configuration">The configuration to apply</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public virtual Task ConnectAsync(ChannelRuntimeConfiguration configuration, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    /// <summary>
    /// Disconnects async
    /// </summary>
    /// <param name="configuration">The configuration to apply</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public virtual Task DisconnectAsync(ChannelRuntimeConfiguration configuration, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    /// <summary>
    /// Normalizes inbound
    /// </summary>
    /// <param name="channelName">The channel name</param>
    /// <param name="payload">The payload</param>
    /// <returns>The resulting channel envelope</returns>
    public abstract ChannelEnvelope NormalizeInbound(string channelName, JsonElement payload);

    /// <summary>
    /// Creates outbound payload
    /// </summary>
    /// <param name="route">The route</param>
    /// <param name="message">The message</param>
    /// <returns>The resulting json object</returns>
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

    /// <summary>
    /// Executes send outbound async
    /// </summary>
    /// <param name="configuration">The configuration to apply</param>
    /// <param name="route">The route</param>
    /// <param name="message">The message</param>
    /// <param name="payload">The payload</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that resolves to bool</returns>
    public virtual Task<bool> SendOutboundAsync(
        ChannelRuntimeConfiguration configuration,
        ChannelSessionRoute route,
        ChannelOutboundMessage message,
        JsonObject payload,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(false);

    /// <summary>
    /// Gets required string
    /// </summary>
    /// <param name="element">The element</param>
    /// <param name="propertyName">The property name</param>
    /// <returns>The resulting string</returns>
    protected static string GetRequiredString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException($"Channel payload is missing required string property '{propertyName}'.");
        }

        return property.GetString() ?? string.Empty;
    }

    /// <summary>
    /// Gets optional string
    /// </summary>
    /// <param name="element">The element</param>
    /// <param name="propertyName">The property name</param>
    /// <returns>The resulting string</returns>
    protected static string GetOptionalString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
        {
            return string.Empty;
        }

        return property.GetString() ?? string.Empty;
    }

    /// <summary>
    /// Gets optional boolean
    /// </summary>
    /// <param name="element">The element</param>
    /// <param name="propertyName">The property name</param>
    /// <returns>A value indicating whether the operation succeeded</returns>
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

    /// <summary>
    /// Gets optional attachments
    /// </summary>
    /// <param name="element">The element</param>
    /// <param name="propertyName">The property name</param>
    /// <returns>The resulting i read only list channel attachment</returns>
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
