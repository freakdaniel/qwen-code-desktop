using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using QwenCode.App.Models;
using QwenCode.App.Tools;

namespace QwenCode.App.Runtime;

internal static class OpenAiCompatibleProtocol
{
    public const string DefaultDashScopeBaseUrl = "https://dashscope.aliyuncs.com/compatible-mode/v1";

    public static JsonObject BuildPayload(
        string model,
        double temperature,
        string systemPrompt,
        AssistantTurnRequest request,
        AssistantPromptContext promptContext,
        IReadOnlyList<AssistantToolCallResult> toolHistory,
        JsonObject? metadata = null,
        JsonObject? extraBody = null)
    {
        var payload = new JsonObject
        {
            ["model"] = model,
            ["temperature"] = temperature,
            ["stream"] = false,
            ["messages"] = BuildMessages(request, promptContext, toolHistory, systemPrompt)
        };

        if (!request.DisableTools)
        {
            payload["tools"] = BuildTools(request.AllowedToolNames);
            payload["tool_choice"] = "auto";
        }
        else
        {
            payload["tool_choice"] = "none";
        }

        if (metadata is not null && metadata.Count > 0)
        {
            payload["metadata"] = metadata.DeepClone();
        }

        if (extraBody is not null && extraBody.Count > 0)
        {
            MergeObjects(payload, extraBody);
        }

        return payload;
    }

    public static ProviderResponse? TryReadResponse(string payload)
    {
        try
        {
            using var document = JsonDocument.Parse(payload);
            if (!document.RootElement.TryGetProperty("choices", out var choices) ||
                choices.ValueKind != JsonValueKind.Array ||
                choices.GetArrayLength() == 0)
            {
                return null;
            }

            var firstChoice = choices[0];
            if (!firstChoice.TryGetProperty("message", out var message) || message.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var summary = string.Empty;
            if (message.TryGetProperty("content", out var content))
            {
                summary = content.ValueKind switch
                {
                    JsonValueKind.String => content.GetString() ?? string.Empty,
                    JsonValueKind.Array => string.Join(
                        "\n",
                        content.EnumerateArray()
                            .Select(static item =>
                                item.ValueKind == JsonValueKind.Object &&
                                item.TryGetProperty("text", out var text) &&
                                text.ValueKind == JsonValueKind.String
                                    ? text.GetString()
                                    : null)
                            .Where(static item => !string.IsNullOrWhiteSpace(item))),
                    _ => string.Empty
                };
            }

            var toolCalls = message.TryGetProperty("tool_calls", out var toolCallsElement) &&
                            toolCallsElement.ValueKind == JsonValueKind.Array
                ? toolCallsElement.EnumerateArray()
                    .Select(static item =>
                    {
                        if (!item.TryGetProperty("id", out var idProperty) ||
                            idProperty.ValueKind != JsonValueKind.String ||
                            !item.TryGetProperty("function", out var functionProperty) ||
                            functionProperty.ValueKind != JsonValueKind.Object ||
                            !functionProperty.TryGetProperty("name", out var nameProperty) ||
                            nameProperty.ValueKind != JsonValueKind.String)
                        {
                            return null;
                        }

                        var argumentsJson =
                            functionProperty.TryGetProperty("arguments", out var argumentsProperty) &&
                            argumentsProperty.ValueKind == JsonValueKind.String
                                ? argumentsProperty.GetString() ?? "{}"
                                : "{}";

                        return new AssistantToolCall
                        {
                            Id = idProperty.GetString() ?? Guid.NewGuid().ToString("N"),
                            ToolName = nameProperty.GetString() ?? string.Empty,
                            ArgumentsJson = string.IsNullOrWhiteSpace(argumentsJson) ? "{}" : argumentsJson
                        };
                    })
                    .Where(static item => item is not null)
                    .Cast<AssistantToolCall>()
                    .ToArray()
                : [];

            if (toolCalls.Length == 0 && string.IsNullOrWhiteSpace(summary))
            {
                return null;
            }

            return new ProviderResponse(summary.Trim(), toolCalls);
        }
        catch
        {
            return null;
        }
    }

    public static string EnsureChatCompletionsEndpoint(string baseUrlOrEndpoint)
    {
        if (string.IsNullOrWhiteSpace(baseUrlOrEndpoint))
        {
            return string.Empty;
        }

        var trimmed = baseUrlOrEndpoint.Trim().TrimEnd('/');
        return trimmed.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase)
            ? trimmed
            : $"{trimmed}/chat/completions";
    }

    public static bool IsDashScopeEndpoint(string endpointOrBaseUrl)
    {
        if (string.IsNullOrWhiteSpace(endpointOrBaseUrl) ||
            !Uri.TryCreate(endpointOrBaseUrl, UriKind.Absolute, out var uri))
        {
            return false;
        }

        return uri.Host.EndsWith("dashscope.aliyuncs.com", StringComparison.OrdinalIgnoreCase) ||
               uri.Host.EndsWith("dashscope-intl.aliyuncs.com", StringComparison.OrdinalIgnoreCase);
    }

    public static string BuildUserAgent()
    {
        var version = Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ??
                      Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ??
                      "dev";
        return $"QwenCodeDesktop/{version} ({RuntimeInformation.OSDescription}; {RuntimeInformation.OSArchitecture})";
    }

    public static void MergeObjects(JsonObject target, JsonObject source)
    {
        foreach (var property in source)
        {
            if (property.Value is JsonObject sourceObject &&
                target[property.Key] is JsonObject targetObject)
            {
                MergeObjects(targetObject, sourceObject);
                continue;
            }

            target[property.Key] = property.Value?.DeepClone();
        }
    }

    private static JsonArray BuildMessages(
        AssistantTurnRequest request,
        AssistantPromptContext promptContext,
        IReadOnlyList<AssistantToolCallResult> toolHistory,
        string systemPrompt)
    {
        var toolHistorySection = toolHistory.Count == 0
            ? "No native tool results have been recorded for this turn yet."
            : string.Join(
                Environment.NewLine + Environment.NewLine,
                toolHistory.Select(static item =>
                    $$"""
Tool: {{item.Execution.ToolName}}
Status: {{item.Execution.Status}}
Approval: {{item.Execution.ApprovalState}}
Arguments: {{item.ToolCall.ArgumentsJson}}
Output:
{{item.Execution.Output}}
Error:
{{item.Execution.ErrorMessage}}
Changed files:
                    {{string.Join(Environment.NewLine, item.Execution.ChangedFiles)}}
"""));
        var projectSummarySection = BuildProjectSummarySection(promptContext.ProjectSummary);

        var userContent = $$"""
{{promptContext.SessionSummary}}

Current turn prompt:
{{request.Prompt}}

{{projectSummarySection}}

History highlights:
{{string.Join(Environment.NewLine, promptContext.HistoryHighlights)}}

Workspace context files:
{{string.Join(Environment.NewLine + Environment.NewLine, promptContext.ContextFiles)}}

Native tool loop history:
{{toolHistorySection}}

Write a concise desktop assistant response for this session turn.
Mention command or tool outcomes when relevant.
If approval is still needed, state that explicitly.
""";

        var messages = new JsonArray
        {
            new JsonObject
            {
                ["role"] = "system",
                ["content"] = systemPrompt
            }
        };

        foreach (var message in promptContext.Messages)
        {
            messages.Add(new JsonObject
            {
                ["role"] = message.Role,
                ["content"] = message.Content
            });
        }

        messages.Add(new JsonObject
        {
            ["role"] = "user",
            ["content"] = userContent
        });

        return messages;
    }

    private static string BuildProjectSummarySection(ProjectSummarySnapshot? projectSummary)
    {
        if (projectSummary is null || !projectSummary.HasHistory)
        {
            return "Project summary:\nNo project summary file is available for this workspace.";
        }

        var pendingTasks = projectSummary.PendingTasks.Count == 0
            ? "No pending tasks captured in PROJECT_SUMMARY.md."
            : string.Join(Environment.NewLine, projectSummary.PendingTasks.Select(static task => $"- {task}"));

        return $$"""
Project summary:
Source: {{projectSummary.FilePath}}
Updated: {{(string.IsNullOrWhiteSpace(projectSummary.TimeAgo) ? projectSummary.TimestampUtc.ToString("u") : projectSummary.TimeAgo)}}
Overall goal:
{{projectSummary.OverallGoal}}

Current plan:
{{projectSummary.CurrentPlan}}

Pending tasks:
{{pendingTasks}}
""";
    }

    private static JsonArray BuildTools(IReadOnlyList<string> allowedToolNames)
    {
        var allowed = allowedToolNames.Count == 0
            ? ToolContractCatalog.Implemented
            : ToolContractCatalog.Implemented
                .Where(tool => allowedToolNames.Contains(tool.Name, StringComparer.OrdinalIgnoreCase))
                .ToArray();

        return new JsonArray(
            allowed
                .Select(static tool =>
                    (JsonNode)new JsonObject
                    {
                        ["type"] = "function",
                        ["function"] = new JsonObject
                        {
                            ["name"] = tool.Name,
                            ["description"] = DescribeTool(tool.Name),
                            ["parameters"] = BuildToolParameters(tool.Name)
                        }
                    })
                .ToArray());
    }

    private static JsonObject BuildToolParameters(string toolName) =>
        toolName switch
        {
            "read_file" => new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["file_path"] = new JsonObject
                    {
                        ["type"] = "string",
                        ["description"] = "Absolute path to the file inside the workspace."
                    },
                    ["offset"] = new JsonObject
                    {
                        ["type"] = "integer",
                        ["description"] = "Zero-based line offset."
                    },
                    ["limit"] = new JsonObject
                    {
                        ["type"] = "integer",
                        ["description"] = "Maximum number of lines to read."
                    }
                },
                ["required"] = new JsonArray("file_path")
            },
            "list_directory" => new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["path"] = new JsonObject
                    {
                        ["type"] = "string",
                        ["description"] = "Absolute directory path inside the workspace."
                    }
                },
                ["required"] = new JsonArray("path")
            },
            "glob" => new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["path"] = new JsonObject
                    {
                        ["type"] = "string",
                        ["description"] = "Optional absolute search root inside the workspace."
                    },
                    ["pattern"] = new JsonObject
                    {
                        ["type"] = "string",
                        ["description"] = "Glob pattern to match."
                    }
                },
                ["required"] = new JsonArray("pattern")
            },
            "grep_search" => new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["path"] = new JsonObject
                    {
                        ["type"] = "string",
                        ["description"] = "Optional absolute search root inside the workspace."
                    },
                    ["pattern"] = new JsonObject
                    {
                        ["type"] = "string",
                        ["description"] = "Regex pattern to search for."
                    },
                    ["glob"] = new JsonObject
                    {
                        ["type"] = "string",
                        ["description"] = "Optional glob filter for matching files."
                    },
                    ["limit"] = new JsonObject
                    {
                        ["type"] = "integer",
                        ["description"] = "Maximum number of matches to return."
                    }
                },
                ["required"] = new JsonArray("pattern")
            },
            "run_shell_command" => new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["command"] = new JsonObject
                    {
                        ["type"] = "string",
                        ["description"] = "Shell command to execute."
                    },
                    ["directory"] = new JsonObject
                    {
                        ["type"] = "string",
                        ["description"] = "Optional working directory inside the workspace."
                    }
                },
                ["required"] = new JsonArray("command")
            },
            "write_file" => new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["file_path"] = new JsonObject
                    {
                        ["type"] = "string",
                        ["description"] = "Absolute path to the file inside the workspace."
                    },
                    ["content"] = new JsonObject
                    {
                        ["type"] = "string",
                        ["description"] = "Full file content to write."
                    }
                },
                ["required"] = new JsonArray("file_path", "content")
            },
            "edit" => new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["file_path"] = new JsonObject
                    {
                        ["type"] = "string",
                        ["description"] = "Absolute path to the file inside the workspace."
                    },
                    ["old_string"] = new JsonObject
                    {
                        ["type"] = "string",
                        ["description"] = "Existing text to replace."
                    },
                    ["new_string"] = new JsonObject
                    {
                        ["type"] = "string",
                        ["description"] = "Replacement text."
                    },
                    ["replace_all"] = new JsonObject
                    {
                        ["type"] = "boolean",
                        ["description"] = "Replace all matches instead of just the first one."
                    }
                },
                ["required"] = new JsonArray("file_path", "old_string", "new_string")
            },
            _ => new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject()
            }
        };

    private static string DescribeTool(string toolName) =>
        toolName switch
        {
            "read_file" => "Read a file from the current workspace.",
            "list_directory" => "List files and directories inside the workspace.",
            "glob" => "Find workspace files using a glob pattern.",
            "grep_search" => "Search workspace files using a regex pattern.",
            "run_shell_command" => "Run a shell command inside the workspace.",
            "write_file" => "Write a full file inside the workspace.",
            "edit" => "Replace text inside a workspace file.",
            _ => "Native workspace tool."
        };

    internal sealed record ProviderResponse(string Summary, IReadOnlyList<AssistantToolCall> ToolCalls);
}
