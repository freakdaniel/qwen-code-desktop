using System.Collections.Concurrent;

namespace QwenCode.Core.Permissions;

/// <summary>
/// In-memory implementation of session-scoped permission rules.
/// </summary>
public sealed class ApprovalSessionRuleStore : IApprovalSessionRuleStore
{
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> allowRules =
        new(StringComparer.Ordinal);

    /// <inheritdoc />
    public void AddAllowRule(string sessionId, string rule)
    {
        if (string.IsNullOrWhiteSpace(sessionId) || string.IsNullOrWhiteSpace(rule))
        {
            return;
        }

        var rules = allowRules.GetOrAdd(sessionId, static _ => new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase));
        rules.TryAdd(rule.Trim(), 0);
    }

    /// <inheritdoc />
    public ApprovalProfile Apply(ApprovalProfile profile, string? sessionId)
    {
        var sessionAllowRules = ListAllowRules(sessionId ?? string.Empty);
        if (sessionAllowRules.Count == 0)
        {
            return profile;
        }

        return new ApprovalProfile
        {
            DefaultMode = profile.DefaultMode,
            ConfirmShellCommands = profile.ConfirmShellCommands,
            ConfirmFileEdits = profile.ConfirmFileEdits,
            AllowRules = profile.AllowRules
                .Concat(sessionAllowRules)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            AskRules = profile.AskRules,
            DenyRules = profile.DenyRules
        };
    }

    /// <inheritdoc />
    public IReadOnlyList<string> ListAllowRules(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId) ||
            !allowRules.TryGetValue(sessionId, out var rules))
        {
            return [];
        }

        return rules.Keys
            .OrderBy(static rule => rule, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
