using QwenCode.App.Models;

namespace QwenCode.App.Output;

public interface IOutputFormatter
{
    string Format<T>(T value, OutputFormat format);
}
