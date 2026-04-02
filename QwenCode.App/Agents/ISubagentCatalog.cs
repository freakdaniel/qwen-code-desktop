using QwenCode.App.Models;

namespace QwenCode.App.Agents;

public interface ISubagentCatalog
{
    IReadOnlyList<SubagentDescriptor> ListAgents(WorkspacePaths paths);

    SubagentDescriptor? FindAgent(WorkspacePaths paths, string name);
}
