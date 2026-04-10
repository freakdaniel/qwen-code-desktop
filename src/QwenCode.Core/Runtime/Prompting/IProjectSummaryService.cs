using QwenCode.Core.Models;

namespace QwenCode.Core.Runtime;

/// <summary>
/// Defines the contract for Project Summary Service
/// </summary>
public interface IProjectSummaryService
{
    /// <summary>
    /// Reads value
    /// </summary>
    /// <param name="runtimeProfile">The runtime profile</param>
    /// <returns>The resulting project summary snapshot?</returns>
    ProjectSummarySnapshot? Read(QwenRuntimeProfile runtimeProfile);
}
