using QwenCode.Core.Models;

namespace QwenCode.App.Desktop;

/// <summary>
/// Defines the contract for Desktop Session Projection Service
/// </summary>
public interface IDesktopSessionProjectionService
{
    /// <summary>
    /// Occurs when Session Event
    /// </summary>
    event EventHandler<DesktopSessionEvent>? SessionEvent;

    /// <summary>
    /// Gets active turns async
    /// </summary>
    /// <returns>A task that resolves to i read only list active turn state</returns>
    Task<IReadOnlyList<ActiveTurnState>> GetActiveTurnsAsync();

    /// <summary>
    /// Gets session async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to desktop session detail?</returns>
    Task<DesktopSessionDetail?> GetSessionAsync(GetDesktopSessionRequest request);

    /// <summary>
    /// Removes session async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to remove desktop session result</returns>
    Task<RemoveDesktopSessionResult> RemoveSessionAsync(RemoveDesktopSessionRequest request);

    /// <summary>
    /// Executes native tool async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to native tool execution result</returns>
    Task<NativeToolExecutionResult> ExecuteNativeToolAsync(ExecuteNativeToolRequest request);

    /// <summary>
    /// Starts session turn async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to desktop session turn result</returns>
    Task<DesktopSessionTurnResult> StartSessionTurnAsync(StartDesktopSessionTurnRequest request);

    /// <summary>
    /// Approves pending tool async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to desktop session turn result</returns>
    Task<DesktopSessionTurnResult> ApprovePendingToolAsync(ApproveDesktopSessionToolRequest request);

    /// <summary>
    /// Executes answer pending question async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to desktop session turn result</returns>
    Task<DesktopSessionTurnResult> AnswerPendingQuestionAsync(AnswerDesktopSessionQuestionRequest request);

    /// <summary>
    /// Cancels session turn async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to cancel desktop session turn result</returns>
    Task<CancelDesktopSessionTurnResult> CancelSessionTurnAsync(CancelDesktopSessionTurnRequest request);

    /// <summary>
    /// Executes resume interrupted turn async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to desktop session turn result</returns>
    Task<DesktopSessionTurnResult> ResumeInterruptedTurnAsync(ResumeInterruptedTurnRequest request);

    /// <summary>
    /// Executes dismiss interrupted turn async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to dismiss interrupted turn result</returns>
    Task<DismissInterruptedTurnResult> DismissInterruptedTurnAsync(DismissInterruptedTurnRequest request);
}
