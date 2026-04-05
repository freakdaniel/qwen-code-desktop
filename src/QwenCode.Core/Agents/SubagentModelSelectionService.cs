using QwenCode.App.Models;

namespace QwenCode.App.Agents;

/// <summary>
/// Represents the Subagent Model Selection Service
/// </summary>
public sealed class SubagentModelSelectionService : ISubagentModelSelectionService
{
    private static readonly HashSet<string> KnownAuthTypes =
    [
        "openai",
        "qwen-oauth",
        "qwen_oauth",
        "qwen-compatible",
        "anthropic",
        "gemini",
        "vertex-ai",
        "coding-plan"
    ];

    /// <summary>
    /// Executes parse
    /// </summary>
    /// <param name="modelSelector">The model selector</param>
    /// <param name="parentAuthType">The parent auth type</param>
    /// <returns>The resulting subagent model selection</returns>
    public SubagentModelSelection Parse(string modelSelector, string parentAuthType)
    {
        var trimmed = modelSelector?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmed) ||
            string.Equals(trimmed, "inherit", StringComparison.OrdinalIgnoreCase))
        {
            return new SubagentModelSelection
            {
                AuthType = parentAuthType,
                Inherits = true
            };
        }

        var colonIndex = trimmed.IndexOf(':');
        if (colonIndex < 0)
        {
            return new SubagentModelSelection
            {
                AuthType = parentAuthType,
                ModelId = trimmed,
                Inherits = false
            };
        }

        var maybeAuthType = trimmed[..colonIndex].Trim();
        var modelId = trimmed[(colonIndex + 1)..].Trim();
        if (!KnownAuthTypes.Contains(maybeAuthType))
        {
            return new SubagentModelSelection
            {
                AuthType = parentAuthType,
                ModelId = trimmed,
                Inherits = false
            };
        }

        if (string.IsNullOrWhiteSpace(modelId))
        {
            throw new InvalidOperationException("Model selector must include a model ID after the auth type prefix.");
        }

        return new SubagentModelSelection
        {
            AuthType = maybeAuthType,
            ModelId = modelId,
            Inherits = false
        };
    }
}
