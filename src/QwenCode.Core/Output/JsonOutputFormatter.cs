using System.Text.Json;
using QwenCode.App.Models;

namespace QwenCode.App.Output;

/// <summary>
/// Represents the Json Output Formatter
/// </summary>
public sealed class JsonOutputFormatter : IOutputFormatter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    /// <summary>
    /// Executes format
    /// </summary>
    /// <typeparam name="T">The type of t</typeparam>
    /// <param name="value">The value</param>
    /// <param name="format">The format</param>
    /// <returns>The resulting string</returns>
    public string Format<T>(T value, OutputFormat format) =>
        format switch
        {
            OutputFormat.Json => JsonSerializer.Serialize(value, JsonOptions),
            OutputFormat.StreamJson => JsonSerializer.Serialize(value, JsonOptions),
            _ => throw new InvalidOperationException($"Formatter '{nameof(JsonOutputFormatter)}' does not support '{format}'.")
        };
}
