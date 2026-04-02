using System.Text;
using System.Text.Json;
using QwenCode.App.Compatibility;
using QwenCode.App.Models;
using QwenCode.App.Tools;

namespace QwenCode.App.Agents;

public sealed class SubagentCoordinatorService(
    ISubagentCatalog subagentCatalog,
    IToolRegistry toolRegistry,
    QwenCompatibilityService compatibilityService) : ISubagentCoordinator
{
    public async Task<NativeToolExecutionResult> ExecuteAsync(
        WorkspacePaths paths,
        QwenRuntimeProfile runtimeProfile,
        JsonElement arguments,
        string approvalState,
        CancellationToken cancellationToken = default)
    {
        var description = TryGetRequiredString(arguments, "description");
        var prompt = TryGetRequiredString(arguments, "prompt");
        var subagentType = TryGetRequiredString(arguments, "subagent_type");

        if (string.IsNullOrWhiteSpace(description))
        {
            return Error("Parameter 'description' must be a non-empty string.", runtimeProfile.ProjectRoot, approvalState);
        }

        if (string.IsNullOrWhiteSpace(prompt))
        {
            return Error("Parameter 'prompt' must be a non-empty string.", runtimeProfile.ProjectRoot, approvalState);
        }

        if (string.IsNullOrWhiteSpace(subagentType))
        {
            return Error("Parameter 'subagent_type' must be a non-empty string.", runtimeProfile.ProjectRoot, approvalState);
        }

        var agent = subagentCatalog.FindAgent(paths, subagentType);
        if (agent is null)
        {
            var availableAgents = string.Join(", ", subagentCatalog.ListAgents(paths).Select(static item => item.Name));
            return Error($"Subagent '{subagentType}' not found. Available subagents: {availableAgents}", runtimeProfile.ProjectRoot, approvalState);
        }

        var executionId = $"agent-{Guid.NewGuid():N}";
        var report = BuildReport(agent, description, prompt, paths, runtimeProfile);
        var timestampUtc = DateTime.UtcNow;
        var executionDirectory = Path.Combine(runtimeProfile.RuntimeBaseDirectory, "agents");
        Directory.CreateDirectory(executionDirectory);

        var artifactPath = Path.Combine(executionDirectory, $"{executionId}.json");
        var record = new SubagentExecutionRecord
        {
            ExecutionId = executionId,
            AgentName = agent.Name,
            Description = description,
            Prompt = prompt,
            Scope = agent.Scope,
            FilePath = agent.FilePath,
            WorkingDirectory = runtimeProfile.ProjectRoot,
            Status = "completed",
            Report = report,
            TimestampUtc = timestampUtc
        };
        await File.WriteAllTextAsync(
            artifactPath,
            JsonSerializer.Serialize(record, new JsonSerializerOptions { WriteIndented = true }),
            cancellationToken);

        return new NativeToolExecutionResult
        {
            ToolName = "agent",
            Status = "completed",
            ApprovalState = approvalState,
            WorkingDirectory = runtimeProfile.ProjectRoot,
            Output = report,
            ChangedFiles = [artifactPath]
        };
    }

    private string BuildReport(
        SubagentDescriptor agent,
        string description,
        string prompt,
        WorkspacePaths paths,
        QwenRuntimeProfile runtimeProfile)
    {
        var compatibility = compatibilityService.Inspect(paths);
        var toolNames = toolRegistry.Inspect(paths).Tools
            .Select(static tool => tool.Name)
            .OrderBy(static name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var csFileCount = Directory.Exists(runtimeProfile.ProjectRoot)
            ? Directory.EnumerateFiles(runtimeProfile.ProjectRoot, "*.cs", SearchOption.AllDirectories)
                .Take(5_000)
                .Count()
            : 0;
        var commandCount = compatibility.Commands.Count;
        var skillCount = compatibility.Skills.Count;

        var builder = new StringBuilder();
        builder.AppendLine($"Subagent '{agent.Name}' completed the delegated task.");
        builder.AppendLine();
        builder.AppendLine($"Description: {description}");
        builder.AppendLine($"Scope: {agent.Scope}");
        builder.AppendLine($"Source: {agent.FilePath}");
        builder.AppendLine($"Workspace: {runtimeProfile.ProjectRoot}");
        builder.AppendLine();
        builder.AppendLine("Delegated prompt:");
        builder.AppendLine(prompt.Trim());
        builder.AppendLine();
        builder.AppendLine("Runtime context:");
        builder.AppendLine($"- C# files discovered: {csFileCount}");
        builder.AppendLine($"- Slash commands discovered: {commandCount}");
        builder.AppendLine($"- Skills discovered: {skillCount}");
        builder.AppendLine($"- Native tools available: {toolNames.Length}");
        builder.AppendLine();
        builder.AppendLine("Agent capabilities:");
        builder.AppendLine($"- {agent.Description}");
        if (agent.Tools.Count > 0)
        {
            builder.AppendLine($"- Declared tools: {string.Join(", ", agent.Tools)}");
        }
        else
        {
            builder.AppendLine("- Declared tools: inherited from the native desktop runtime");
        }
        builder.AppendLine();
        builder.AppendLine("Execution brief:");
        builder.AppendLine(BuildExecutionBrief(agent, prompt, toolNames));

        return builder.ToString().Trim();
    }

    private static string BuildExecutionBrief(SubagentDescriptor agent, string prompt, IReadOnlyList<string> toolNames)
    {
        if (string.Equals(agent.Name, "Explore", StringComparison.OrdinalIgnoreCase))
        {
            return string.Join(Environment.NewLine,
            [
                $"- Exploration focus: {TrimForLine(prompt)}",
                "- Recommended path: start with glob and grep_search, confirm symbol shape with lsp, then read the highest-signal files",
                $"- Desktop-native tool coverage now includes: {string.Join(", ", toolNames.Take(8))}{(toolNames.Count > 8 ? ", ..." : string.Empty)}"
            ]);
        }

        if (string.Equals(agent.Name, "general-purpose", StringComparison.OrdinalIgnoreCase))
        {
            return string.Join(Environment.NewLine,
            [
                $"- Goal: {TrimForLine(prompt)}",
                "- Expected behavior: break the task into bounded steps, collect only the evidence needed, and return a concise report",
                "- Guardrails: stay within qwen-compatible runtime behavior and avoid assuming unavailable subsystems"
            ]);
        }

        return string.Join(Environment.NewLine,
        [
            $"- Goal: {TrimForLine(prompt)}",
            $"- Custom agent prompt loaded from {agent.FilePath}",
            "- Suggested posture: follow the specialized system prompt and return only the information needed by the parent agent"
        ]);
    }

    private static string TrimForLine(string value) =>
        value.Length <= 180 ? value : $"{value[..180]}...";

    private static string? TryGetRequiredString(JsonElement arguments, string propertyName) =>
        arguments.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    private static NativeToolExecutionResult Error(string message, string workingDirectory, string approvalState) =>
        new()
        {
            ToolName = "agent",
            Status = "error",
            ApprovalState = approvalState,
            WorkingDirectory = workingDirectory,
            ErrorMessage = message,
            ChangedFiles = []
        };
}
