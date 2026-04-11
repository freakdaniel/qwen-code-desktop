using QwenCode.Core.Tools;

namespace QwenCode.Core.Runtime;

internal static class OpenAiCompatibleProtocol
{
    private static readonly HashSet<string> StrictQwenCompatibleToolNames =
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

    private static readonly string[] PreferredQwenCompatibleToolOrder =
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
            payload["tools"] = BuildTools(
                request.AllowedToolNames,
                string.Equals(providerFlavor, "dashscope", StringComparison.OrdinalIgnoreCase));
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
        foreach (var toolName in ResolveQwenCompatibleToolOrder(byName.Keys))
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

    private static JsonArray BuildTools(
        IReadOnlyList<string> allowedToolNames,
        bool strictQwenCompatibleTools = false)
    {
        var allowed = allowedToolNames.Count == 0
            ? ToolContractCatalog.Implemented
            : ToolContractCatalog.Implemented
                .Where(tool => allowedToolNames.Contains(tool.Name, StringComparer.OrdinalIgnoreCase))
                .ToArray();
        if (strictQwenCompatibleTools)
        {
            allowed = allowed
                .Where(tool => StrictQwenCompatibleToolNames.Contains(tool.Name))
                .ToArray();
        }

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
            "read_file" => BuildObjectSchema(
                [("file_path", BuildStringSchema("Absolute path to the file inside the workspace.")),
                 ("offset", BuildIntegerSchema("Zero-based line offset.")),
                 ("limit", BuildIntegerSchema("Maximum number of lines to read."))],
                "file_path"),
            "list_directory" => BuildObjectSchema(
                [("path", BuildStringSchema("Absolute directory path inside the workspace."))],
                "path"),
            "glob" => BuildObjectSchema(
                [("path", BuildStringSchema("Optional absolute search root inside the workspace.")),
                 ("pattern", BuildStringSchema("Glob pattern to match."))],
                "pattern"),
            "grep_search" => BuildObjectSchema(
                [("path", BuildStringSchema("Optional absolute search root inside the workspace.")),
                 ("pattern", BuildStringSchema("Regex pattern to search for.")),
                 ("glob", BuildStringSchema("Optional glob filter for matching files.")),
                 ("limit", BuildIntegerSchema("Maximum number of matches to return."))],
                "pattern"),
            "run_shell_command" => BuildObjectSchema(
                [("command", BuildStringSchema("Shell command to execute.")),
                 ("directory", BuildStringSchema("Optional working directory inside the workspace."))],
                "command"),
            "write_file" => BuildObjectSchema(
                [("file_path", BuildStringSchema("Absolute path to the file inside the workspace.")),
                 ("content", BuildStringSchema("Full file content to write."))],
                "file_path",
                "content"),
            "edit" => BuildObjectSchema(
                [("file_path", BuildStringSchema("Absolute path to the file inside the workspace.")),
                 ("old_string", BuildStringSchema("Existing text to replace.")),
                 ("new_string", BuildStringSchema("Replacement text.")),
                 ("replace_all", BuildBooleanSchema("Replace all matches instead of just the first one."))],
                "file_path",
                "old_string",
                "new_string"),
            "todo_write" => BuildObjectSchema(
                [("todos", BuildTodoArraySchema()),
                 ("session_id", BuildStringSchema("Optional session id used to associate the todo list with a chat session. Keep exactly one task in progress when the work is active."))],
                "todos"),
            "task_create" => BuildObjectSchema(
                [("subject", BuildStringSchema("Short title for the task.")),
                 ("description", BuildStringSchema("Detailed description of the task.")),
                 ("active_form", BuildStringSchema("Present-continuous progress label such as 'Running tests'.")),
                 ("owner", BuildStringSchema("Optional owner or responsible agent name.")),
                 ("metadata", BuildJsonObjectSchema("Optional task metadata object.")),
                 ("session_id", BuildStringSchema("Optional session id used to scope the task list. Use task records for multi-step or multi-agent work that should stay visible across turns."))],
                "subject",
                "description"),
            "task_list" => BuildObjectSchema(
                [("session_id", BuildStringSchema("Optional session id used to scope the task list.")),
                 ("status", BuildEnumStringSchema("Optional status filter.", "pending", "in_progress", "completed", "cancelled"))],
                Array.Empty<string>()),
            "task_get" => BuildObjectSchema(
                [("task_id", BuildStringSchema("Task identifier to retrieve.")),
                 ("session_id", BuildStringSchema("Optional session id used to scope the task list."))],
                "task_id"),
            "task_update" => BuildObjectSchema(
                [("task_id", BuildStringSchema("Task identifier to update.")),
                 ("subject", BuildStringSchema("Updated task subject.")),
                 ("description", BuildStringSchema("Updated task description.")),
                 ("active_form", BuildStringSchema("Updated present-continuous progress label.")),
                 ("status", BuildEnumStringSchema("Updated task status.", "pending", "in_progress", "completed", "cancelled")),
                 ("owner", BuildStringSchema("Updated owner or responsible agent name.")),
                 ("add_blocks", BuildStringArraySchema("Task ids that this task blocks.")),
                 ("add_blocked_by", BuildStringArraySchema("Task ids that block this task.")),
                 ("metadata", BuildJsonObjectSchema("Task metadata object that replaces the previous metadata when supplied.")),
                 ("session_id", BuildStringSchema("Optional session id used to scope the task list."))],
                "task_id"),
            "task_stop" => BuildObjectSchema(
                [("task_id", BuildStringSchema("Task identifier to cancel or stop.")),
                 ("session_id", BuildStringSchema("Optional session id used to scope the task list."))],
                "task_id"),
            "save_memory" => BuildObjectSchema(
                [("fact", BuildStringSchema("Specific durable fact or preference to remember.")),
                 ("scope", BuildEnumStringSchema("Where to save the memory. Use only for durable facts, not transient task state.", "global", "project"))],
                "fact"),
            "agent" => BuildObjectSchema(
                [("description", BuildStringSchema("Short 3-5 word summary of the delegated task.")),
                 ("prompt", BuildStringSchema("Detailed instructions for the subagent. Include goal, relevant files, constraints, and what success looks like.")),
                 ("subagent_type", BuildStringSchema("Registered subagent type to launch. Choose a type that already matches the delegated workflow.")),
                 ("task_id", BuildStringSchema("Optional linked orchestration task id to claim, update, and complete automatically around the delegated run."))],
                "description",
                "prompt",
                "subagent_type"),
            "arena" => BuildObjectSchema(
                [("task", BuildStringSchema("Task prompt to run across multiple competing arena agents.")),
                 ("models", BuildArenaModelsSchema()),
                 ("action", BuildStringSchema("Optional arena control action such as status, continue, cancel, cleanup, discard, select_winner, or apply_winner.")),
                 ("session_id", BuildStringSchema("Optional existing arena session id for follow-up arena actions.")),
                 ("task_id", BuildStringSchema("Optional linked orchestration task id. Arena claims it while comparison is running and completes it when a winner is applied.")),
                 ("cleanup", BuildBooleanSchema("Whether arena worktrees should be cleaned up after completion.")),
                 ("base_branch", BuildStringSchema("Optional git base branch for arena worktrees.")),
                 ("allowed_tools", BuildStringArraySchema("Optional allowlist of tools arena agents may use.")),
                 ("winner", BuildStringSchema("Optional winning agent name for select/apply actions.")),
                 ("agent_name", BuildStringSchema("Alias for the winning agent name when selecting or applying a winner."))],
                "task"),
            "skill" => BuildObjectSchema(
                [("skill", BuildStringSchema("Name of the skill to load.")),
                 ("skill_name", BuildStringSchema("Alias for the skill name when the caller uses skill_name instead of skill."))],
                "skill"),
            "tool_search" => BuildObjectSchema(
                [("query", BuildStringSchema("Free-text query describing the kind of tool you need. Use this before guessing when the best tool is unclear.")),
                 ("kind", BuildEnumStringSchema("Optional tool kind filter.", "read", "modify", "execute", "coordination", "automation", "control")),
                 ("approval_state", BuildEnumStringSchema("Optional approval filter.", "allow", "ask", "deny")),
                 ("limit", BuildIntegerSchema("Maximum number of matching tools to return."))],
                Array.Empty<string>()),
            "exit_plan_mode" => BuildObjectSchema([], Array.Empty<string>()),
            "web_fetch" => BuildObjectSchema(
                [("url", BuildStringSchema("Absolute http:// or https:// URL to fetch.")),
                 ("prompt", BuildStringSchema("Optional extraction or analysis prompt for the fetched content. Ask for the specific facts you need from that page."))],
                "url"),
            "web_search" => BuildObjectSchema(
                [("query", BuildStringSchema("Search query to send to the configured web search provider. Include a concrete year for recent releases, docs, or news when relevant.")),
                 ("provider", BuildStringSchema("Optional provider override when multiple web search providers are configured."))],
                "query"),
            "mcp-client" => BuildObjectSchema(
                [("server_name", BuildStringSchema("Connected MCP server name.")),
                 ("prompt_name", BuildStringSchema("Prompt name to invoke from that MCP server.")),
                 ("uri", BuildStringSchema("Resource URI to read from that MCP server.")),
                 ("arguments", BuildJsonObjectSchema("Optional structured arguments for MCP prompt invocation or describe requests."))],
                Array.Empty<string>()),
            "mcp-tool" => BuildObjectSchema(
                [("server_name", BuildStringSchema("Connected MCP server name that owns the tool.")),
                 ("tool_name", BuildStringSchema("Exact MCP tool name to invoke.")),
                 ("arguments", BuildJsonObjectSchema("Tool-specific JSON object passed through to the MCP tool."))],
                "server_name",
                "tool_name"),
            "lsp" => BuildObjectSchema(
                [("operation", BuildEnumStringSchema(
                    "LSP operation to execute.",
                    "documentSymbol",
                    "workspaceSymbol",
                    "hover",
                    "goToDefinition",
                    "goToImplementation",
                    "findReferences",
                    "diagnostics",
                    "workspaceDiagnostics",
                    "prepareCallHierarchy",
                    "incomingCalls",
                    "outgoingCalls",
                    "codeActions")),
                 ("file_path", BuildStringSchema("Absolute source file path for file-scoped LSP operations.")),
                 ("line", BuildIntegerSchema("Zero-based line number for symbol lookup operations.")),
                 ("character", BuildIntegerSchema("Zero-based character offset for symbol lookup operations.")),
                 ("query", BuildStringSchema("Workspace symbol query string.")),
                 ("limit", BuildIntegerSchema("Maximum number of results to return.")),
                 ("includeDeclaration", BuildBooleanSchema("Whether reference lookups should include declaration locations."))],
                "operation"),
            "ask_user_question" => BuildObjectSchema(
                [("questions", BuildQuestionArraySchema())],
                "questions"),
            "cron_create" => BuildObjectSchema(
                [("cron", BuildStringSchema("Cron expression describing when to run the task.")),
                 ("prompt", BuildStringSchema("Prompt to execute when the schedule fires.")),
                 ("recurring", BuildBooleanSchema("Whether the schedule should repeat. Defaults to true."))],
                "cron",
                "prompt"),
            "cron_list" => BuildObjectSchema([], Array.Empty<string>()),
            "cron_delete" => BuildObjectSchema(
                [("id", BuildStringSchema("Identifier of the cron job to cancel."))],
                "id"),
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
            "run_shell_command" => "Run a shell command inside the workspace for build, test, git, or environment tasks.",
            "write_file" => "Write a full file inside the workspace.",
            "edit" => "Replace text inside a workspace file with a targeted edit.",
            "todo_write" => "Create or update a structured todo list for the current coding task. Keep progress visible and update it as you work.",
            "task_create" => "Create a richer session-scoped task record for multi-step orchestration, ownership, and progress tracking.",
            "task_list" => "List session-scoped orchestration tasks and their current status.",
            "task_get" => "Read the full details for a specific session-scoped task.",
            "task_update" => "Update a session-scoped task's status, ownership, dependencies, description, or active execution state.",
            "task_stop" => "Stop or cancel a session-scoped task that should no longer continue.",
            "save_memory" => "Persist a durable fact or preference to global or project memory. Do not use it for transient task state.",
            "agent" => "Launch a specialized subagent for delegated or parallel work, while keeping final synthesis in the parent agent.",
            "arena" => "Run the same task across multiple arena agents or manage an existing arena session.",
            "skill" => "Load a predefined skill workflow or instructions bundle by name.",
            "tool_search" => "Search the native desktop tool catalog by intent, kind, or approval state before guessing at the best tool.",
            "exit_plan_mode" => "Exit plan mode after preparing a concrete plan for the user.",
            "web_fetch" => "Fetch a specific URL and return the page contents or prompt-focused extraction once you know the likely source.",
            "web_search" => "Search the web for current or external information and return sourced results, especially when facts may have changed.",
            "mcp-client" => "Inspect connected MCP servers, invoke MCP prompts, or read MCP resources.",
            "mcp-tool" => "Execute a concrete tool exposed by a connected MCP server.",
            "lsp" => "Query Roslyn code intelligence such as symbols, definitions, references, diagnostics, or call hierarchy.",
            "ask_user_question" => "Pause execution and ask the user one or more structured follow-up questions.",
            "cron_create" => "Create a session-scoped recurring or one-shot automation job.",
            "cron_list" => "List active session-scoped automation jobs.",
            "cron_delete" => "Cancel a session-scoped automation job.",
            _ => "Native tool available in this desktop runtime."
        };

    private static IReadOnlyList<string> ResolveQwenCompatibleToolOrder(IEnumerable<string> availableToolNames)
    {
        var available = new HashSet<string>(availableToolNames, StringComparer.OrdinalIgnoreCase);
        var ordered = new List<string>();

        foreach (var toolName in PreferredQwenCompatibleToolOrder)
        {
            if (available.Remove(toolName))
            {
                ordered.Add(toolName);
            }
        }

        ordered.AddRange(available.OrderBy(static item => item, StringComparer.OrdinalIgnoreCase));
        return ordered;
    }

    private static JsonObject BuildObjectSchema(
        IEnumerable<(string Name, JsonNode Schema)> properties,
        params string[] requiredProperties)
    {
        var propertyObject = new JsonObject();
        foreach (var (name, schema) in properties)
        {
            propertyObject[name] = schema;
        }

        var result = new JsonObject
        {
            ["type"] = "object",
            ["properties"] = propertyObject
        };

        if (requiredProperties.Length > 0)
        {
            result["required"] = new JsonArray(requiredProperties.Select(static property => (JsonNode)JsonValue.Create(property)!).ToArray());
        }

        return result;
    }

    private static JsonObject BuildStringSchema(string description) =>
        new()
        {
            ["type"] = "string",
            ["description"] = description
        };

    private static JsonObject BuildStringArraySchema(string description) =>
        new()
        {
            ["type"] = "array",
            ["description"] = description,
            ["items"] = BuildStringSchema("String item.")
        };

    private static JsonObject BuildIntegerSchema(string description) =>
        new()
        {
            ["type"] = "integer",
            ["description"] = description
        };

    private static JsonObject BuildBooleanSchema(string description) =>
        new()
        {
            ["type"] = "boolean",
            ["description"] = description
        };

    private static JsonObject BuildJsonObjectSchema(string description) =>
        new()
        {
            ["type"] = "object",
            ["description"] = description
        };

    private static JsonObject BuildEnumStringSchema(string description, params string[] values) =>
        new()
        {
            ["type"] = "string",
            ["description"] = description,
            ["enum"] = new JsonArray(values.Select(static value => (JsonNode)JsonValue.Create(value)!).ToArray())
        };

    private static JsonObject BuildTodoArraySchema() =>
        new()
        {
            ["type"] = "array",
            ["description"] = "Full updated todo list for the session.",
            ["items"] = BuildObjectSchema(
                [("id", BuildStringSchema("Stable todo identifier.")),
                 ("content", BuildStringSchema("Short description of the task.")),
                 ("status", BuildEnumStringSchema("Current task state.", "pending", "in_progress", "completed"))],
                "id",
                "content",
                "status")
        };

    private static JsonObject BuildQuestionArraySchema() =>
        new()
        {
            ["type"] = "array",
            ["description"] = "Questions to present to the user for clarification or a decision.",
            ["items"] = BuildObjectSchema(
                [("header", BuildStringSchema("Short UI label for the question.")),
                 ("question", BuildStringSchema("Prompt shown to the user.")),
                 ("multiSelect", BuildBooleanSchema("Whether the user may choose multiple options.")),
                 ("options", new JsonObject
                 {
                     ["type"] = "array",
                     ["description"] = "Available answer options.",
                     ["items"] = BuildObjectSchema(
                         [("label", BuildStringSchema("Visible option label.")),
                          ("description", BuildStringSchema("Short explanation of the option."))],
                         "label",
                         "description")
                 })],
                "header",
                "question",
                "options")
        };

    private static JsonObject BuildArenaModelsSchema() =>
        new()
        {
            ["type"] = "array",
            ["description"] = "Arena competitor model descriptors.",
            ["items"] = BuildObjectSchema(
                [("model", BuildStringSchema("Model identifier for the arena competitor.")),
                 ("agent_name", BuildStringSchema("Optional display name for this arena competitor.")),
                 ("prompt", BuildStringSchema("Optional extra prompt or strategy instructions for this competitor."))],
                "model")
        };

    internal sealed record ProviderResponse(string Summary, IReadOnlyList<AssistantToolCall> ToolCalls);
}
