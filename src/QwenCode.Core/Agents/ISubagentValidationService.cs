using QwenCode.App.Models;

namespace QwenCode.App.Agents;

/// <summary>
/// Defines the contract for Subagent Validation Service
/// </summary>
public interface ISubagentValidationService
{
    /// <summary>
    /// Validates value
    /// </summary>
    /// <param name="descriptor">The descriptor</param>
    /// <returns>The resulting subagent validation result</returns>
    SubagentValidationResult Validate(SubagentDescriptor descriptor);
}
