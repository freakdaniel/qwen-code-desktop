namespace QwenCode.Core.Models;

/// <summary>
/// Represents the request used to answer a pending question through a direct-connect session.
/// </summary>
public sealed class AnswerDirectConnectSessionQuestionRequest
{
    /// <summary>
    /// Gets or sets the direct-connect session id.
    /// </summary>
    public string DirectConnectSessionId { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the wrapped answer request.
    /// </summary>
    public AnswerDesktopSessionQuestionRequest Answer { get; init; } = new();
}
