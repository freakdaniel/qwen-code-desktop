using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using QwenCode.App.Models;
using QwenCode.App.Tools;

namespace QwenCode.App.Runtime;

internal static class OpenAiCompatibleProtocol
{
    private static readonly string[] SupportedQwenCompatibleTools =
    [
        "agent",
        "skill",
        "list_directory",
        "read_file",
        "grep_search",
        "glob",
        "edit",
        "write_file",
        "run_shell_command",
        "save_memory",
        "todo_write",
        "ask_user_question",
        "exit_plan_mode",
        "web_fetch",
        "web_search"
    ];

    /// <summary>
    /// Represents the Default Dash Scope Base Url
    /// </summary>
    public const string DefaultDashScopeBaseUrl = "https://dashscope.aliyuncs.com/compatible-mode/v1";

    /// <summary>
    /// Builds payload
    /// </summary>
    /// <param name="model">The model</param>
    /// <param name="temperature">The temperature</param>
    /// <param name="maxOutputTokens">The max output tokens</param>
    /// <param name="systemPrompt">The system prompt</param>
    /// <param name="request">The request payload</param>
    /// <param name="promptContext">The prompt context</param>
    /// <param name="toolHistory">The tool history</param>
    /// <param name="metadata">The metadata</param>
    /// <param name="extraBody">The extra body</param>
    /// <param name="providerFlavor">The provider flavor</param>
    /// <returns>The resulting json object</returns>
    public static JsonObject BuildPayload(
        string model,
        double temperature,
        int maxOutputTokens,
        string systemPrompt,
        AssistantTurnRequest request,
        AssistantPromptContext promptContext,
        IReadOnlyList<AssistantToolCallResult> toolHistory,
        JsonObject? metadata = null,
        JsonObject? extraBody = null,
        string providerFlavor = "")
    {
        var payload = new JsonObject
        {
            ["model"] = model,
            ["temperature"] = temperature,
            ["stream"] = false,
            ["max_tokens"] = maxOutputTokens,
            ["messages"] = BuildMessages(request, promptContext, toolHistory, systemPrompt, model, providerFlavor)
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

    /// <summary>
    /// Attempts to read response
    /// </summary>
    /// <param name="payload">The payload</param>
    /// <returns>The resulting provider response?</returns>
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

    /// <summary>
    /// Executes ensure chat completions endpoint
    /// </summary>
    /// <param name="baseUrlOrEndpoint">The base url or endpoint</param>
    /// <returns>The resulting string</returns>
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

    /// <summary>
    /// Executes is dash scope endpoint
    /// </summary>
    /// <param name="endpointOrBaseUrl">The endpoint or base url</param>
    /// <returns>A value indicating whether the operation succeeded</returns>
    public static bool IsDashScopeEndpoint(string endpointOrBaseUrl)
    {
        if (string.IsNullOrWhiteSpace(endpointOrBaseUrl) ||
            !Uri.TryCreate(endpointOrBaseUrl, UriKind.Absolute, out var uri))
        {
            return false;
        }

        return uri.Host.EndsWith("dashscope.aliyuncs.com", StringComparison.OrdinalIgnoreCase) ||
               uri.Host.EndsWith("dashscope-intl.aliyuncs.com", StringComparison.OrdinalIgnoreCase) ||
               uri.Host.EndsWith("portal.qwen.ai", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Executes is open router endpoint
    /// </summary>
    /// <param name="endpointOrBaseUrl">The endpoint or base url</param>
    /// <returns>A value indicating whether the operation succeeded</returns>
    public static bool IsOpenRouterEndpoint(string endpointOrBaseUrl) =>
        HasHostSuffix(endpointOrBaseUrl, "openrouter.ai");

    /// <summary>
    /// Executes is deep seek endpoint
    /// </summary>
    /// <param name="endpointOrBaseUrl">The endpoint or base url</param>
    /// <returns>A value indicating whether the operation succeeded</returns>
    public static bool IsDeepSeekEndpoint(string endpointOrBaseUrl) =>
        HasHostSuffix(endpointOrBaseUrl, "api.deepseek.com");

    /// <summary>
    /// Executes is model scope endpoint
    /// </summary>
    /// <param name="endpointOrBaseUrl">The endpoint or base url</param>
    /// <returns>A value indicating whether the operation succeeded</returns>
    public static bool IsModelScopeEndpoint(string endpointOrBaseUrl) =>
        HasHostSuffix(endpointOrBaseUrl, "api.modelscope.cn") ||
        HasHostSuffix(endpointOrBaseUrl, "modelscope");

    /// <summary>
    /// Builds user agent
    /// </summary>
    /// <returns>The resulting string</returns>
    public static string BuildUserAgent()
    {
        var version = Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ??
                      Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ??
                      "dev";
        var platform = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "win32"
            : RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
                ? "darwin"
                : RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
                    ? "linux"
                    : "unknown";
        var architecture = RuntimeInformation.OSArchitecture.ToString().ToLowerInvariant();
        return $"QwenCode/{version} ({platform}; {architecture})";
    }

    /// <summary>
    /// Executes merge objects
    /// </summary>
    /// <param name="target">The target</param>
    /// <param name="source">The source</param>
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

    /// <summary>
    /// Normalizes payload for provider flavor
    /// </summary>
    /// <param name="payload">The payload</param>
    /// <param name="providerFlavor">The provider flavor</param>
    /// <param name="streaming">The streaming</param>
    public static void NormalizePayloadForProviderFlavor(JsonObject payload, string providerFlavor, bool streaming)
    {
        if (streaming)
        {
            payload["stream_options"] = new JsonObject
            {
                ["include_usage"] = true
            };
        }
        else
        {
            payload.Remove("stream_options");
        }

        if (string.Equals(providerFlavor, "deepseek", StringComparison.OrdinalIgnoreCase) &&
            payload["messages"] is JsonArray messages)
        {
            FlattenDeepSeekMessageContent(messages);
        }

        if (string.Equals(providerFlavor, "modelscope", StringComparison.OrdinalIgnoreCase) && !streaming)
        {
            payload.Remove("stream_options");
        }
    }

    /// <summary>
    /// Normalizes payload for qwen-compatible requests routed through Qwen OAuth / DashScope endpoints.
    /// </summary>
    /// <param name="payload">The payload</param>
    /// <param name="streaming">The streaming</param>
    /// <param name="disableTools">Whether tools are disabled for the current request</param>
    public static void NormalizePayloadForQwenCompatible(
        JsonObject payload,
        bool streaming,
        bool disableTools)
    {
        NormalizePayloadForProviderFlavor(payload, "dashscope", streaming);

        // The official Qwen CLI only sends model/messages/tools for chat turns and
        // lets the backend determine output sizing/default sampling behavior.
        payload.Remove("max_tokens");
        payload.Remove("temperature");

        if (payload["tools"] is JsonArray tools)
        {
            NormalizeQwenCompatibleTools(tools);
        }

        if (!disableTools)
        {
            payload.Remove("tool_choice");
        }
    }

    private static void NormalizeQwenCompatibleTools(JsonArray tools)
    {
        var byName = tools
            .OfType<JsonObject>()
            .Select(tool => new
            {
                Tool = tool,
                Name = tool["function"]?["name"]?.GetValue<string>()
            })
            .Where(item => !string.IsNullOrWhiteSpace(item.Name))
            .ToDictionary(item => item.Name!, item => item.Tool, StringComparer.OrdinalIgnoreCase);

        tools.Clear();
        foreach (var toolName in SupportedQwenCompatibleTools)
        {
            if (byName.TryGetValue(toolName, out var tool))
            {
                tools.Add(tool);
            }
        }
    }

    private static JsonArray BuildMessages(
        AssistantTurnRequest request,
        AssistantPromptContext promptContext,
        IReadOnlyList<AssistantToolCallResult> toolHistory,
        string systemPrompt,
        string model,
        string providerFlavor)
    {
        var resolvedSystemPrompt = NativeAssistantRuntimePromptBuilder.BuildSystemPrompt(
            request,
            promptContext,
            NormalizeCustomPrompt(systemPrompt),
            NormalizeCustomPrompt(request.SystemPromptOverride),
            model,
            providerFlavor);

        var messages = new JsonArray
        {
            new JsonObject
            {
                ["role"] = "system",
                ["content"] = resolvedSystemPrompt
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
            ["content"] = NativeAssistantRuntimePromptBuilder.BuildCurrentTurnUserMessage(request, promptContext, toolHistory)
        });

        foreach (var toolResult in toolHistory)
        {
            messages.Add(new JsonObject
            {
                ["role"] = "assistant",
                ["content"] = string.Empty,
                ["tool_calls"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["id"] = toolResult.ToolCall.Id,
                        ["type"] = "function",
                        ["function"] = new JsonObject
                        {
                            ["name"] = toolResult.ToolCall.ToolName,
                            ["arguments"] = string.IsNullOrWhiteSpace(toolResult.ToolCall.ArgumentsJson)
                                ? "{}"
                                : toolResult.ToolCall.ArgumentsJson
                        }
                    }
                }
            });

            messages.Add(new JsonObject
            {
                ["role"] = "tool",
                ["tool_call_id"] = toolResult.ToolCall.Id,
                ["content"] = BuildToolResultContent(toolResult)
            });
        }

        return messages;
    }

    private static string NormalizeCustomPrompt(string prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            return string.Empty;
        }

        return string.Equals(prompt, NativeAssistantRuntimePromptBuilder.DefaultSystemPrompt, StringComparison.Ordinal)
            ? string.Empty
            : prompt.Trim();
    }

    private static void FlattenDeepSeekMessageContent(JsonArray messages)
    {
        foreach (var node in messages.OfType<JsonObject>())
        {
            if (node["content"] is not JsonArray parts)
            {
                continue;
            }

            var text = string.Join(
                Environment.NewLine + Environment.NewLine,
                parts.Select(static part =>
                {
                    if (part is JsonValue raw && raw.TryGetValue<string>(out var stringValue))
                    {
                        return stringValue ?? string.Empty;
                    }

                    if (part is JsonObject objectPart &&
                        string.Equals(objectPart["type"]?.GetValue<string>(), "text", StringComparison.OrdinalIgnoreCase))
                    {
                        return objectPart["text"]?.GetValue<string>() ?? string.Empty;
                    }

                    if (part is JsonObject unsupported)
                    {
                        var type = unsupported["type"]?.GetValue<string>() ?? "unknown";
                        return $"[Unsupported content type: {type}]";
                    }

                    return string.Empty;
                }).Where(static item => !string.IsNullOrWhiteSpace(item)));

            node["content"] = text;
        }
    }

    private static bool HasHostSuffix(string endpointOrBaseUrl, string suffix)
    {
        if (string.IsNullOrWhiteSpace(endpointOrBaseUrl) ||
            !Uri.TryCreate(endpointOrBaseUrl, UriKind.Absolute, out var uri))
        {
            return false;
        }

        return uri.Host.Contains(suffix, StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildToolResultContent(AssistantToolCallResult toolResult)
    {
        var output = string.IsNullOrWhiteSpace(toolResult.Execution.Output)
            ? "(empty)"
            : toolResult.Execution.Output.Trim();
        var error = string.IsNullOrWhiteSpace(toolResult.Execution.ErrorMessage)
            ? "(none)"
            : toolResult.Execution.ErrorMessage.Trim();
        var changedFiles = toolResult.Execution.ChangedFiles.Count == 0
            ? "(none)"
            : string.Join(Environment.NewLine, toolResult.Execution.ChangedFiles);
        var pendingQuestions = toolResult.Execution.Questions.Count == 0
            ? "(none)"
            : string.Join(Environment.NewLine, toolResult.Execution.Questions.Select(static question => $"{question.Header}: {question.Question}"));

        return $$"""
Tool: {{toolResult.Execution.ToolName}}
Status: {{toolResult.Execution.Status}}
Approval: {{toolResult.Execution.ApprovalState}}
Exit code: {{toolResult.Execution.ExitCode}}
Output:
{{output}}
Error:
{{error}}
Changed files:
{{changedFiles}}
Pending questions:
{{pendingQuestions}}
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
