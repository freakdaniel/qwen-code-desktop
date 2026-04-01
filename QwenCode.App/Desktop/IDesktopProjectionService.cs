using QwenCode.App.Models;

namespace QwenCode.App.Desktop;

public interface IDesktopProjectionService
{
    event EventHandler<DesktopStateChangedEvent>? StateChanged;

    Task<AppBootstrapPayload> GetBootstrapAsync();

    Task<DesktopSessionDetail?> GetSessionAsync(string sessionId);

    Task<DesktopStateChangedEvent> SetLocaleAsync(string locale);

    Task<QwenNativeToolExecutionResult> ExecuteNativeToolAsync(ExecuteNativeToolRequest request);

    Task<DesktopSessionTurnResult> StartSessionTurnAsync(StartDesktopSessionTurnRequest request);

    Task<DesktopSessionTurnResult> ApprovePendingToolAsync(ApproveDesktopSessionToolRequest request);
}
