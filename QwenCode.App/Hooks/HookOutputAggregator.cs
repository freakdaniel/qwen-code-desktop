using QwenCode.App.Models;

namespace QwenCode.App.Hooks;

public sealed class HookOutputAggregator
{
    public HookOutput Aggregate(IReadOnlyList<HookExecutionResult> executions)
    {
        if (executions.Count == 0)
        {
            return new HookOutput
            {
                Decision = "allow"
            };
        }

        var reasons = new List<string>();
        var additionalContexts = new List<string>();
        string systemMessage = string.Empty;
        string modifiedPrompt = string.Empty;
        bool hasBlock = false;
        bool hasAllow = false;

        foreach (var execution in executions)
        {
            if (!string.IsNullOrWhiteSpace(execution.ErrorMessage))
            {
                reasons.Add(execution.ErrorMessage);
                continue;
            }

            var output = execution.Output;
            if (output is null)
            {
                continue;
            }

            if (string.Equals(output.Decision, "block", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(output.Decision, "deny", StringComparison.OrdinalIgnoreCase))
            {
                hasBlock = true;
            }
            else if (string.Equals(output.Decision, "allow", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(output.Decision, "approve", StringComparison.OrdinalIgnoreCase))
            {
                hasAllow = true;
            }

            if (!string.IsNullOrWhiteSpace(output.Reason))
            {
                reasons.Add(output.Reason);
            }

            if (!string.IsNullOrWhiteSpace(output.AdditionalContext))
            {
                additionalContexts.Add(output.AdditionalContext);
            }

            if (!string.IsNullOrWhiteSpace(output.SystemMessage))
            {
                systemMessage = output.SystemMessage;
            }

            if (!string.IsNullOrWhiteSpace(output.ModifiedPrompt))
            {
                modifiedPrompt = output.ModifiedPrompt;
            }
        }

        return new HookOutput
        {
            Decision = hasBlock
                ? "block"
                : hasAllow
                    ? "allow"
                    : string.Empty,
            Reason = string.Join(
                Environment.NewLine,
                reasons.Where(static value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal)),
            AdditionalContext = string.Join(
                Environment.NewLine,
                additionalContexts.Where(static value => !string.IsNullOrWhiteSpace(value))),
            SystemMessage = systemMessage,
            ModifiedPrompt = modifiedPrompt
        };
    }
}
