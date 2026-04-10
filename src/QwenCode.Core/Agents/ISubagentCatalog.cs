using QwenCode.Core.Models;

namespace QwenCode.Core.Agents;

/// <summary>
/// Defines the contract for Subagent Catalog
/// </summary>
public interface ISubagentCatalog
{
    /// <summary>
    /// Lists agents
    /// </summary>
    /// <param name="paths">The paths to process</param>
    /// <returns>The resulting i read only list subagent descriptor</returns>
    IReadOnlyList<SubagentDescriptor> ListAgents(WorkspacePaths paths);

    /// <summary>
    /// Executes find agent
    /// </summary>
    /// <param name="paths">The paths to process</param>
    /// <param name="name">The name</param>
    /// <returns>The resulting subagent descriptor?</returns>
    SubagentDescriptor? FindAgent(WorkspacePaths paths, string name);
}
