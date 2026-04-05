using System.Text.Json;
using QwenCode.App.Models;

namespace QwenCode.App.Output;

public sealed class JsonOutputFormatter : IOutputFormatter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public string Format<T>(T value, OutputFormat format) =>
        format switch
        {
            OutputFormat.Json => JsonSerializer.Serialize(value, JsonOptions),
            OutputFormat.StreamJson => JsonSerializer.Serialize(value, JsonOptions),
            _ => throw new InvalidOperationException($"Formatter '{nameof(JsonOutputFormatter)}' does not support '{format}'.")
        };
}
