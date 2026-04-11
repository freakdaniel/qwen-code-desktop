using QwenCode.Core.Models;

namespace QwenCode.Core.Permissions;

/// <summary>
/// Represents the Approval Policy Service
/// </summary>
public sealed class ApprovalPolicyService : IApprovalPolicyEngine
{
    /// <summary>
    /// Executes evaluate
    /// </summary>
    /// <param name="context">The context</param>
    /// <param name="approvalProfile">The approval profile</param>
    /// <returns>The resulting approval decision</returns>
    public ApprovalDecision Evaluate(
        ApprovalCheckContext context,
        ApprovalProfile approvalProfile)
    {
        if (PermissionRuleParser.IsShellTool(context.ToolName) &&
            !string.IsNullOrWhiteSpace(context.Command) &&
            PermissionRuleParser.SplitCompoundCommand(context.Command) is { Count: > 1 } subCommands)
        {
            return EvaluateCompoundShellCommand(context, approvalProfile, subCommands);
        }

        var contexts = BuildCandidateContexts(context);
        var primaryContext = contexts[0];

        if (TryMatchRule(approvalProfile.DenyRules, contexts, out var denyRule))
        {
            var parsedDenyRule = PermissionRuleParser.Parse(denyRule);
            return new ApprovalDecision
            {
                State = "deny",
                Reason = $"Blocked by explicit qwen-compatible deny rule '{denyRule}'.",
                MatchedRule = denyRule,
                IsWholeToolDenyRule = string.IsNullOrWhiteSpace(parsedDenyRule.Specifier)
            };
        }

        if (TryMatchRule(approvalProfile.AskRules, contexts, out var askRule))
        {
            return new ApprovalDecision
            {
                State = "ask",
                Reason = $"Requires confirmation due to explicit ask rule '{askRule}'.",
                MatchedRule = askRule,
                IsExplicitAskRule = true
            };
        }

        if (TryMatchRule(approvalProfile.AllowRules, [primaryContext], out var allowRule))
        {
            return new ApprovalDecision
            {
                State = "allow",
                Reason = $"Allowed by explicit compatibility rule '{allowRule}'.",
                MatchedRule = allowRule
            };
        }

        var derivedContexts = contexts.Skip(1).ToArray();
        if (derivedContexts.Length > 0 &&
            derivedContexts.All(candidate => TryMatchRule(approvalProfile.AllowRules, [candidate], out _)))
        {
            return new ApprovalDecision
            {
                State = "allow",
                Reason = "Allowed by qwen-compatible shell semantics across extracted virtual operations."
            };
        }

        if (string.Equals(primaryContext.ToolName, "web_search", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(primaryContext.ToolName, "web_fetch", StringComparison.OrdinalIgnoreCase))
        {
            return new ApprovalDecision
            {
                State = "allow",
                Reason = "Web search and fetch tools are allowed by default in the desktop shell unless an explicit rule overrides them."
            };
        }

        if (string.Equals(primaryContext.ToolName, "run_shell_command", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(primaryContext.Command))
        {
            if (ShellOperationAnalyzer.ContainsCommandSubstitution(primaryContext.Command))
            {
                return new ApprovalDecision
                {
                    State = "deny",
                    Reason = "Blocked because the shell command uses command substitution."
                };
            }

            if (approvalProfile.ConfirmShellCommands == true)
            {
                return new ApprovalDecision
                {
                    State = "ask",
                    Reason = "Requires confirmation due to shell confirmation setting."
                };
            }

            if (!string.IsNullOrWhiteSpace(primaryContext.WorkingDirectory) &&
                ShellOperationAnalyzer.IsReadOnlyCommand(primaryContext.Command, primaryContext.WorkingDirectory))
            {
                return new ApprovalDecision
                {
                    State = "allow",
                    Reason = "Allowed by qwen-compatible read-only shell command analysis."
                };
            }
        }

        if (primaryContext.ToolName is "edit" or "write_file" &&
            approvalProfile.ConfirmFileEdits == true)
        {
            return new ApprovalDecision
            {
                State = "ask",
                Reason = "Requires confirmation due to file edit confirmation setting."
            };
        }

        var decision = approvalProfile.DefaultMode.ToLowerInvariant() switch
        {
            "yolo" => ("allow", "Allowed by YOLO default mode."),
            "auto-edit" when primaryContext.Kind is "modify" or "read" or "control" => ("allow", "Allowed by auto-edit default mode."),
            "auto-edit" => ("ask", "Still requires confirmation outside file-edit flow."),
            "plan" when primaryContext.Kind is "modify" or "execute" or "automation" or "coordination" => ("deny", "Blocked by plan mode semantics."),
            "plan" => ("allow", "Read-style tool remains available in plan mode."),
            "default" when primaryContext.Kind is "read" or "control" => ("allow", "Read/control tool stays available in default mode."),
            "default" => ("ask", "Requires confirmation in default mode."),
            _ => ("ask", "Falls back to cautious confirmation semantics.")
        };

        return new ApprovalDecision
        {
            State = decision.Item1,
            Reason = decision.Item2
        };
    }

    private ApprovalDecision EvaluateCompoundShellCommand(
        ApprovalCheckContext context,
        ApprovalProfile approvalProfile,
        IReadOnlyList<string> subCommands)
    {
        ApprovalDecision? mostRestrictive = null;
        var mostRestrictiveCommand = string.Empty;

        foreach (var subCommand in subCommands)
        {
            var decision = Evaluate(context with { Command = subCommand }, approvalProfile);
            if (mostRestrictive is null ||
                GetDecisionPriority(decision.State) > GetDecisionPriority(mostRestrictive.State))
            {
                mostRestrictive = decision;
                mostRestrictiveCommand = subCommand;
            }

            if (string.Equals(mostRestrictive.State, "deny", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }
        }

        if (mostRestrictive is null)
        {
            return new ApprovalDecision
            {
                State = "ask",
                Reason = "Requires confirmation because the compound shell command could not be evaluated."
            };
        }

        return new ApprovalDecision
        {
            State = mostRestrictive.State,
            Reason = string.Equals(mostRestrictive.State, "allow", StringComparison.OrdinalIgnoreCase)
                ? "Allowed because every compound shell command segment is allowed."
                : $"Compound shell command requires '{mostRestrictive.State}' because segment '{mostRestrictiveCommand}' matched: {mostRestrictive.Reason}"
        };
    }

    private static int GetDecisionPriority(string state) =>
        state.ToLowerInvariant() switch
        {
            "deny" => 3,
            "ask" => 2,
            "allow" => 0,
            _ => 1
        };

    private static bool TryMatchRule(
        IReadOnlyList<string> rules,
        IReadOnlyList<ApprovalCheckContext> contexts,
        out string matchedRule)
    {
        foreach (var rule in rules.Where(static rule => !string.IsNullOrWhiteSpace(rule)))
        {
            var parsedRule = PermissionRuleParser.Parse(rule);
            if (contexts.Any(context => PermissionRuleParser.Matches(parsedRule, context)))
            {
                matchedRule = rule;
                return true;
            }
        }

        matchedRule = string.Empty;
        return false;
    }

    private static IReadOnlyList<ApprovalCheckContext> BuildCandidateContexts(ApprovalCheckContext context)
    {
        var primaryContext = context with { ToolName = PermissionRuleParser.ResolveToolName(context.ToolName) };
        var contexts = new List<ApprovalCheckContext> { primaryContext };

        if (string.Equals(primaryContext.ToolName, "run_shell_command", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(primaryContext.Command) &&
            !string.IsNullOrWhiteSpace(primaryContext.WorkingDirectory))
        {
            contexts.AddRange(
                ShellOperationAnalyzer.ExtractOperations(primaryContext.Command, primaryContext.WorkingDirectory)
                    .Select(operation => new ApprovalCheckContext
                    {
                        ToolName = operation.VirtualTool,
                        Kind = ClassifyKind(operation.VirtualTool),
                        ProjectRoot = primaryContext.ProjectRoot,
                        WorkingDirectory = primaryContext.WorkingDirectory,
                        FilePath = operation.FilePath,
                        Domain = operation.Domain
                    }));
        }

        return contexts;
    }

    private static string ClassifyKind(string toolName) => toolName switch
    {
        "read_file" or "glob" or "grep_search" or "list_directory" => "read",
        "edit" or "write_file" or "todo_write" or "save_memory" => "modify",
        "run_shell_command" or "web_fetch" or "web_search" or "mcp-tool" or "lsp" => "execute",
        "mcp-client" => "read",
        "agent" or "skill" or "ask_user_question" => "coordination",
        "exit_plan_mode" => "control",
        "cron_create" or "cron_list" or "cron_delete" => "automation",
        _ => "other"
    };
}
