using QwenCode.App.Models;

namespace QwenCode.App.Output;

/// <summary>
/// Represents the Output Formatter
/// </summary>
/// <param name="textOutputFormatter">The text output formatter</param>
/// <param name="jsonOutputFormatter">The json output formatter</param>
public sealed class OutputFormatter(
    TextOutputFormatter textOutputFormatter,
    JsonOutputFormatter jsonOutputFormatter) : IOutputFormatter
{
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
            OutputFormat.Text => textOutputFormatter.Format(value, format),
            OutputFormat.Json or OutputFormat.StreamJson => jsonOutputFormatter.Format(value, format),
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unsupported output format.")
        };
}
