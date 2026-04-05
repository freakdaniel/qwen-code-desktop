using QwenCode.App.Models;

namespace QwenCode.App.Ide;

/// <summary>
/// Defines the contract for Ide Detection Service
/// </summary>
public interface IIdeDetectionService
{
    /// <summary>
    /// Detects value
    /// </summary>
    /// <param name="processCommand">The process command</param>
    /// <param name="environment">The environment</param>
    /// <param name="overrideInfo">The override info</param>
    /// <returns>The resulting ide info?</returns>
    IdeInfo? Detect(string processCommand, IReadOnlyDictionary<string, string>? environment = null, IdeInfo? overrideInfo = null);
}
