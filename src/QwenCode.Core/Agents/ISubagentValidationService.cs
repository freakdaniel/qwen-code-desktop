using QwenCode.Core.Models;

namespace QwenCode.Core.Agents;

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
