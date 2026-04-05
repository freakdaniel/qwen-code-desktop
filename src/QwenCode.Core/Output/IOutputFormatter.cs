using QwenCode.App.Models;

namespace QwenCode.App.Output;

/// <summary>
/// Defines the contract for Output Formatter
/// </summary>
public interface IOutputFormatter
{
    /// <summary>
    /// Executes format
    /// </summary>
    /// <typeparam name="T">The type of t</typeparam>
    /// <param name="value">The value</param>
    /// <param name="format">The format</param>
    /// <returns>The resulting string</returns>
    string Format<T>(T value, OutputFormat format);
}
