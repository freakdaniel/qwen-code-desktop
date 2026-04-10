using QwenCode.Core.Models;

namespace QwenCode.Core.Sessions;

/// <summary>
/// Defines the contract for Session Host
/// </summary>
public interface ISessionHost
{
    /// <summary>
    /// Occurs when Session Event
    /// </summary>
    event EventHandler<DesktopSessionEvent>? SessionEvent;

    /// <summary>
    /// Starts turn async
    /// </summary>
    /// <param name="paths">The paths to process</param>
    /// <param name="request">The request payload</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that resolves to desktop session turn result</returns>
    Task<DesktopSessionTurnResult> StartTurnAsync(
        WorkspacePaths paths,
        StartDesktopSessionTurnRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Approves pending tool async
    /// </summary>
    /// <param name="paths">The paths to process</param>
    /// <param name="request">The request payload</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that resolves to desktop session turn result</returns>
    Task<DesktopSessionTurnResult> ApprovePendingToolAsync(
        WorkspacePaths paths,
        ApproveDesktopSessionToolRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes answer pending question async
    /// </summary>
    /// <param name="paths">The paths to process</param>
    /// <param name="request">The request payload</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that resolves to desktop session turn result</returns>
    Task<DesktopSessionTurnResult> AnswerPendingQuestionAsync(
        WorkspacePaths paths,
        AnswerDesktopSessionQuestionRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels turn async
    /// </summary>
    /// <param name="paths">The paths to process</param>
    /// <param name="request">The request payload</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that resolves to cancel desktop session turn result</returns>
    Task<CancelDesktopSessionTurnResult> CancelTurnAsync(
        WorkspacePaths paths,
        CancelDesktopSessionTurnRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes resume interrupted turn async
    /// </summary>
    /// <param name="paths">The paths to process</param>
    /// <param name="request">The request payload</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that resolves to desktop session turn result</returns>
    Task<DesktopSessionTurnResult> ResumeInterruptedTurnAsync(
        WorkspacePaths paths,
        ResumeInterruptedTurnRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes dismiss interrupted turn async
    /// </summary>
    /// <param name="paths">The paths to process</param>
    /// <param name="request">The request payload</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that resolves to dismiss interrupted turn result</returns>
    Task<DismissInterruptedTurnResult> DismissInterruptedTurnAsync(
        WorkspacePaths paths,
        DismissInterruptedTurnRequest request,
        CancellationToken cancellationToken = default);
}
