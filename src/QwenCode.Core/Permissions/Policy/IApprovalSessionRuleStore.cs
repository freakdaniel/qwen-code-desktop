namespace QwenCode.Core.Permissions;

/// <summary>
/// Stores in-memory permission rules that only apply to a single desktop session.
/// </summary>
public interface IApprovalSessionRuleStore
{
    /// <summary>
    /// Adds a session-scoped allow rule.
    /// </summary>
    /// <param name="sessionId">The session id.</param>
    /// <param name="rule">The allow rule.</param>
    void AddAllowRule(string sessionId, string rule);

    /// <summary>
    /// Applies session-scoped rules to the supplied profile.
    /// </summary>
    /// <param name="profile">The base profile.</param>
    /// <param name="sessionId">The session id.</param>
    /// <returns>The profile with session-scoped rules merged in.</returns>
    ApprovalProfile Apply(ApprovalProfile profile, string? sessionId);

    /// <summary>
    /// Lists session-scoped allow rules.
    /// </summary>
    /// <param name="sessionId">The session id.</param>
    /// <returns>The allow rules for the session.</returns>
    IReadOnlyList<string> ListAllowRules(string sessionId);
}
