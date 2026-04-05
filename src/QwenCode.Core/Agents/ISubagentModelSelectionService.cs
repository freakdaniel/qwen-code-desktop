using QwenCode.App.Models;

namespace QwenCode.App.Agents;

/// <summary>
/// Defines the contract for Subagent Model Selection Service
/// </summary>
public interface ISubagentModelSelectionService
{
    /// <summary>
    /// Executes parse
    /// </summary>
    /// <param name="modelSelector">The model selector</param>
    /// <param name="parentAuthType">The parent auth type</param>
    /// <returns>The resulting subagent model selection</returns>
    SubagentModelSelection Parse(string modelSelector, string parentAuthType);
}
