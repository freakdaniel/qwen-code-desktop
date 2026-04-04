using QwenCode.App.Models;

namespace QwenCode.App.Output;

public sealed class OutputFormatter(
    TextOutputFormatter textOutputFormatter,
    JsonOutputFormatter jsonOutputFormatter) : IOutputFormatter
{
    public string Format<T>(T value, OutputFormat format) =>
        format switch
        {
            OutputFormat.Text => textOutputFormatter.Format(value, format),
            OutputFormat.Json or OutputFormat.StreamJson => jsonOutputFormatter.Format(value, format),
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unsupported output format.")
        };
}
