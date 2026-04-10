using System.Text.Json;
using QwenCode.Core.Models;

namespace QwenCode.Core.Tools;

/// <summary>
/// Defines the contract for Skill Tool Service
/// </summary>
public interface ISkillToolService
{
    /// <summary>
    /// Loads skill content async
    /// </summary>
    /// <param name="runtimeProfile">The runtime profile</param>
    /// <param name="arguments">The arguments</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that resolves to string</returns>
    Task<string> LoadSkillContentAsync(
        QwenRuntimeProfile runtimeProfile,
        JsonElement arguments,
        CancellationToken cancellationToken = default);
}
