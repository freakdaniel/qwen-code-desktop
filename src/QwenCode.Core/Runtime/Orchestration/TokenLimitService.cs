using System.Text.RegularExpressions;
using QwenCode.App.Options;

namespace QwenCode.App.Runtime;

/// <summary>
/// Represents the Token Limit Service
/// </summary>
public sealed class TokenLimitService : ITokenLimitService
{
    private const int DefaultInputTokenLimit = 131_072;
    private const int DefaultOutputTokenLimit = 32_000;

    private static readonly (Regex Pattern, int Limit)[] InputPatterns =
    [
        (new Regex("^gemini-3", RegexOptions.IgnoreCase | RegexOptions.Compiled), 1_000_000),
        (new Regex("^gemini-", RegexOptions.IgnoreCase | RegexOptions.Compiled), 1_000_000),
        (new Regex("^gpt-5", RegexOptions.IgnoreCase | RegexOptions.Compiled), 272_000),
        (new Regex("^gpt-", RegexOptions.IgnoreCase | RegexOptions.Compiled), 131_072),
        (new Regex("^o\\d", RegexOptions.IgnoreCase | RegexOptions.Compiled), 200_000),
        (new Regex("^claude-", RegexOptions.IgnoreCase | RegexOptions.Compiled), 200_000),
        (new Regex("^qwen3-coder-plus", RegexOptions.IgnoreCase | RegexOptions.Compiled), 1_000_000),
        (new Regex("^qwen3-coder-flash", RegexOptions.IgnoreCase | RegexOptions.Compiled), 1_000_000),
        (new Regex("^qwen3\\.\\d", RegexOptions.IgnoreCase | RegexOptions.Compiled), 1_000_000),
        (new Regex("^qwen-plus-latest$", RegexOptions.IgnoreCase | RegexOptions.Compiled), 1_000_000),
        (new Regex("^qwen-flash-latest$", RegexOptions.IgnoreCase | RegexOptions.Compiled), 1_000_000),
        (new Regex("^coder-model$", RegexOptions.IgnoreCase | RegexOptions.Compiled), 1_000_000),
        (new Regex("^qwen3-max", RegexOptions.IgnoreCase | RegexOptions.Compiled), 262_144),
        (new Regex("^qwen3-coder-", RegexOptions.IgnoreCase | RegexOptions.Compiled), 262_144),
        (new Regex("^qwen", RegexOptions.IgnoreCase | RegexOptions.Compiled), 262_144),
        (new Regex("^deepseek", RegexOptions.IgnoreCase | RegexOptions.Compiled), 131_072),
        (new Regex("^glm-5", RegexOptions.IgnoreCase | RegexOptions.Compiled), 202_752),
        (new Regex("^glm-", RegexOptions.IgnoreCase | RegexOptions.Compiled), 202_752),
        (new Regex("^minimax-m2\\.5", RegexOptions.IgnoreCase | RegexOptions.Compiled), 196_608),
        (new Regex("^minimax-", RegexOptions.IgnoreCase | RegexOptions.Compiled), 200_000),
        (new Regex("^kimi-", RegexOptions.IgnoreCase | RegexOptions.Compiled), 262_144),
        (new Regex("^seed-oss", RegexOptions.IgnoreCase | RegexOptions.Compiled), 524_288)
    ];

    private static readonly (Regex Pattern, int Limit)[] OutputPatterns =
    [
        (new Regex("^gemini-3", RegexOptions.IgnoreCase | RegexOptions.Compiled), 65_536),
        (new Regex("^gemini-", RegexOptions.IgnoreCase | RegexOptions.Compiled), 8_192),
        (new Regex("^gpt-5", RegexOptions.IgnoreCase | RegexOptions.Compiled), 131_072),
        (new Regex("^gpt-", RegexOptions.IgnoreCase | RegexOptions.Compiled), 16_384),
        (new Regex("^o\\d", RegexOptions.IgnoreCase | RegexOptions.Compiled), 131_072),
        (new Regex("^claude-opus-4-6", RegexOptions.IgnoreCase | RegexOptions.Compiled), 131_072),
        (new Regex("^claude-sonnet-4-6", RegexOptions.IgnoreCase | RegexOptions.Compiled), 65_536),
        (new Regex("^claude-", RegexOptions.IgnoreCase | RegexOptions.Compiled), 65_536),
        (new Regex("^qwen3\\.\\d", RegexOptions.IgnoreCase | RegexOptions.Compiled), 65_536),
        (new Regex("^coder-model$", RegexOptions.IgnoreCase | RegexOptions.Compiled), 65_536),
        (new Regex("^qwen", RegexOptions.IgnoreCase | RegexOptions.Compiled), 32_768),
        (new Regex("^deepseek-reasoner", RegexOptions.IgnoreCase | RegexOptions.Compiled), 65_536),
        (new Regex("^deepseek-r1", RegexOptions.IgnoreCase | RegexOptions.Compiled), 65_536),
        (new Regex("^deepseek-chat", RegexOptions.IgnoreCase | RegexOptions.Compiled), 8_192),
        (new Regex("^glm-5", RegexOptions.IgnoreCase | RegexOptions.Compiled), 16_384),
        (new Regex("^glm-4\\.7", RegexOptions.IgnoreCase | RegexOptions.Compiled), 16_384),
        (new Regex("^minimax-m2\\.5", RegexOptions.IgnoreCase | RegexOptions.Compiled), 65_536),
        (new Regex("^kimi-k2\\.5", RegexOptions.IgnoreCase | RegexOptions.Compiled), 32_768)
    ];

    /// <summary>
    /// Resolves value
    /// </summary>
    /// <param name="model">The model</param>
    /// <param name="options">The options</param>
    /// <returns>The resulting resolved token limits</returns>
    public ResolvedTokenLimits Resolve(string model, NativeAssistantRuntimeOptions options)
    {
        var normalized = Normalize(model);
        var inputLimit = options.InputTokenLimit > 0
            ? options.InputTokenLimit.Value
            : ResolveLimit(normalized, InputPatterns, DefaultInputTokenLimit);

        var explicitOutputLimit = TryResolveExplicitOutputLimit(normalized, out var detectedOutputLimit);
        var outputLimit = options.OutputTokenLimit > 0
            ? options.OutputTokenLimit.Value
            : detectedOutputLimit;

        return new ResolvedTokenLimits
        {
            Model = model,
            NormalizedModel = normalized,
            InputTokenLimit = inputLimit,
            OutputTokenLimit = outputLimit,
            HasExplicitOutputLimit = options.OutputTokenLimit > 0 || explicitOutputLimit
        };
    }

    private static bool TryResolveExplicitOutputLimit(string normalizedModel, out int outputLimit)
    {
        foreach (var (pattern, limit) in OutputPatterns)
        {
            if (!pattern.IsMatch(normalizedModel))
            {
                continue;
            }

            outputLimit = limit;
            return true;
        }

        outputLimit = DefaultOutputTokenLimit;
        return false;
    }

    private static int ResolveLimit(string normalizedModel, IEnumerable<(Regex Pattern, int Limit)> patterns, int defaultLimit)
    {
        foreach (var (pattern, limit) in patterns)
        {
            if (pattern.IsMatch(normalizedModel))
            {
                return limit;
            }
        }

        return defaultLimit;
    }

    /// <summary>
    /// Normalizes value
    /// </summary>
    /// <param name="model">The model</param>
    /// <returns>The resulting string</returns>
    public static string Normalize(string model)
    {
        var normalized = (model ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.Empty;
        }

        var slashIndex = normalized.LastIndexOf('/');
        if (slashIndex >= 0 && slashIndex < normalized.Length - 1)
        {
            normalized = normalized[(slashIndex + 1)..];
        }

        var pipeParts = normalized.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (pipeParts.Length > 0)
        {
            normalized = pipeParts[^1];
        }

        var colonParts = normalized.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (colonParts.Length > 0)
        {
            normalized = colonParts[^1];
        }

        normalized = Regex.Replace(normalized, "\\s+", "-");
        normalized = normalized.Replace("-preview", string.Empty, StringComparison.OrdinalIgnoreCase);

        if (!Regex.IsMatch(normalized, "^qwen-(?:plus|flash|vl-max)-latest$") &&
            !Regex.IsMatch(normalized, "^kimi-k2-\\d{4}$"))
        {
            normalized = Regex.Replace(
                normalized,
                "-(?:\\d{4,}|\\d+x\\d+b|v\\d+(?:\\.\\d+)*|(?<=-[^-]+-)\\d+(?:\\.\\d+)+|latest|exp)$",
                string.Empty,
                RegexOptions.IgnoreCase);
        }

        normalized = Regex.Replace(
            normalized,
            "-(?:\\d?bit|int[48]|bf16|fp16|q[45]|quantized)$",
            string.Empty,
            RegexOptions.IgnoreCase);

        return normalized;
    }
}
