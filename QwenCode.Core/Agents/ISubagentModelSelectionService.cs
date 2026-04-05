using QwenCode.App.Models;

namespace QwenCode.App.Agents;

public interface ISubagentModelSelectionService
{
    SubagentModelSelection Parse(string modelSelector, string parentAuthType);
}
