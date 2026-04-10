using QwenCode.Core.Runtime;

namespace QwenCode.Core.Runtime;

/// <summary>
/// Defines the contract for Token Limit Service
/// </summary>
public interface ITokenLimitService
{
    /// <summary>
    /// Resolves value
    /// </summary>
    /// <param name="model">The model</param>
    /// <param name="options">The options</param>
    /// <returns>The resulting resolved token limits</returns>
    ResolvedTokenLimits Resolve(string model, NativeAssistantRuntimeOptions options);
}
