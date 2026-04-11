using QwenCode.Core.Models;

namespace QwenCode.App.Desktop.DirectConnect;

/// <summary>
/// Defines the contract for direct-connect session orchestration.
/// </summary>
public interface IDirectConnectSessionService
{
    /// <summary>
    /// Creates a new direct-connect session.
    /// </summary>
    Task<DirectConnectSessionState> CreateSessionAsync(
        CreateDirectConnectSessionRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists active direct-connect sessions.
    /// </summary>
    Task<IReadOnlyList<DirectConnectSessionState>> ListSessionsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets one direct-connect session snapshot.
    /// </summary>
    Task<DirectConnectSessionState?> GetSessionAsync(
        string directConnectSessionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads buffered session events after the supplied sequence.
    /// </summary>
    Task<DirectConnectSessionEventBatch> ReadEventsAsync(
        string directConnectSessionId,
        long afterSequence = 0,
        int maxCount = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Streams buffered and live session events after the supplied sequence.
    /// </summary>
    IAsyncEnumerable<DirectConnectSessionEventRecord> StreamEventsAsync(
        string directConnectSessionId,
        long afterSequence = 0,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts a turn through a direct-connect session.
    /// </summary>
    Task<DesktopSessionTurnResult> StartTurnAsync(
        string directConnectSessionId,
        StartDesktopSessionTurnRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Approves a pending tool through a direct-connect session.
    /// </summary>
    Task<DesktopSessionTurnResult> ApprovePendingToolAsync(
        string directConnectSessionId,
        ApproveDesktopSessionToolRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Answers a pending question through a direct-connect session.
    /// </summary>
    Task<DesktopSessionTurnResult> AnswerPendingQuestionAsync(
        string directConnectSessionId,
        AnswerDesktopSessionQuestionRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels the active turn through a direct-connect session.
    /// </summary>
    Task<CancelDesktopSessionTurnResult> CancelTurnAsync(
        string directConnectSessionId,
        CancelDesktopSessionTurnRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resumes an interrupted turn through a direct-connect session.
    /// </summary>
    Task<DesktopSessionTurnResult> ResumeInterruptedTurnAsync(
        string directConnectSessionId,
        ResumeInterruptedTurnRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Dismisses an interrupted turn through a direct-connect session.
    /// </summary>
    Task<DismissInterruptedTurnResult> DismissInterruptedTurnAsync(
        string directConnectSessionId,
        DismissInterruptedTurnRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Closes and removes a direct-connect session.
    /// </summary>
    Task<DirectConnectSessionState> CloseSessionAsync(
        string directConnectSessionId,
        CancellationToken cancellationToken = default);
}
