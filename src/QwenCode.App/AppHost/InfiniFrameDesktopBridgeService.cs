using System.Text.Json;
using System.Text.Json.Serialization;
using InfiniFrame;
using Microsoft.Extensions.Logging;
using QwenCode.App.Ipc;

namespace QwenCode.App.AppHost;

/// <summary>
/// Coordinates renderer messaging over InfiniFrame web messages.
/// </summary>
public sealed class InfiniFrameDesktopBridgeService(
    DesktopIpcService desktopIpcService,
    IDesktopWindowBridge windowBridge,
    ILogger<InfiniFrameDesktopBridgeService> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private int _initialized;

    /// <summary>
    /// Attaches the active window and registers outbound event relays.
    /// </summary>
    /// <param name="window">The active window.</param>
    public void Initialize(IInfiniFrameWindow window)
    {
        windowBridge.AttachWindow(window);

        if (Interlocked.Exchange(ref _initialized, 1) != 0)
        {
            return;
        }

        desktopIpcService.RegisterEventChannels((channel, payload) =>
        {
            _ = PublishAsync(new OutboundMessage
            {
                Type = "event",
                Channel = channel,
                Payload = payload
            });
        });
    }

    /// <summary>
    /// Handles an incoming renderer message.
    /// </summary>
    /// <param name="window">The active window.</param>
    /// <param name="message">The serialized message.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task HandleWebMessageAsync(IInfiniFrameWindow window, string message)
    {
        Initialize(window);

        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        InboundMessage? inbound;
        try
        {
            inbound = JsonSerializer.Deserialize<InboundMessage>(message, JsonOptions);
        }
        catch (JsonException exception)
        {
            logger.LogWarning(exception, "Ignoring invalid renderer bridge message");
            return;
        }

        if (inbound is null || string.IsNullOrWhiteSpace(inbound.Type))
        {
            return;
        }

        switch (inbound.Type)
        {
            case "invoke":
                await HandleInvokeAsync(inbound).ConfigureAwait(false);
                break;
            case "command":
                await HandleCommandAsync(inbound).ConfigureAwait(false);
                break;
            default:
                logger.LogDebug("Ignoring unsupported bridge message type {Type}", inbound.Type);
                break;
        }
    }

    private async Task HandleInvokeAsync(InboundMessage inbound)
    {
        if (string.IsNullOrWhiteSpace(inbound.RequestId) || string.IsNullOrWhiteSpace(inbound.Channel))
        {
            return;
        }

        try
        {
            var payloadJson = inbound.Payload?.GetRawText();
            var result = await desktopIpcService.InvokeChannelAsync(inbound.Channel, payloadJson).ConfigureAwait(false);
            await PublishAsync(new OutboundMessage
            {
                Type = "response",
                RequestId = inbound.RequestId,
                Channel = inbound.Channel,
                Payload = result
            }).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Renderer invoke failed for {Channel}", inbound.Channel);
            await PublishAsync(new OutboundMessage
            {
                Type = "response",
                RequestId = inbound.RequestId,
                Channel = inbound.Channel,
                Error = exception.Message
            }).ConfigureAwait(false);
        }
    }

    private async Task HandleCommandAsync(InboundMessage inbound)
    {
        object? result = null;
        string? error = null;

        try
        {
            switch (inbound.Command)
            {
                case "window:minimize":
                    windowBridge.Minimize();
                    result = new { ok = true };
                    break;
                case "window:toggle-maximize":
                    windowBridge.ToggleMaximize();
                    result = new { ok = true };
                    break;
                case "window:begin-drag":
                    windowBridge.BeginDrag();
                    result = new { ok = true };
                    break;
                case "window:begin-resize":
                    windowBridge.BeginResize(inbound.Edge ?? string.Empty);
                    result = new { ok = true };
                    break;
                case "window:close":
                    windowBridge.Close();
                    result = new { ok = true };
                    break;
                case "external:open":
                    result = new { opened = windowBridge.OpenExternalUrl(inbound.Url ?? string.Empty) };
                    break;
                default:
                    error = $"Unsupported command '{inbound.Command}'.";
                    break;
            }
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Renderer command failed for {Command}", inbound.Command);
            error = exception.Message;
        }

        if (!string.IsNullOrWhiteSpace(inbound.RequestId))
        {
            await PublishAsync(new OutboundMessage
            {
                Type = "response",
                RequestId = inbound.RequestId,
                Channel = inbound.Command ?? string.Empty,
                Payload = result,
                Error = error
            }).ConfigureAwait(false);
        }
    }

    private Task PublishAsync(OutboundMessage message)
        => windowBridge.PublishAsync(JsonSerializer.Serialize(message, JsonOptions));

    private sealed class InboundMessage
    {
        public string Type { get; set; } = string.Empty;

        public string RequestId { get; set; } = string.Empty;

        public string Channel { get; set; } = string.Empty;

        public JsonElement? Payload { get; set; }

        public string Command { get; set; } = string.Empty;

        public string Url { get; set; } = string.Empty;

        public string Edge { get; set; } = string.Empty;
    }

    private sealed class OutboundMessage
    {
        public string Type { get; set; } = string.Empty;

        public string RequestId { get; set; } = string.Empty;

        public string Channel { get; set; } = string.Empty;

        public object? Payload { get; set; }

        public string? Error { get; set; }
    }
}
