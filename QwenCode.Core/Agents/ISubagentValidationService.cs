using QwenCode.App.Models;

namespace QwenCode.App.Agents;

public interface ISubagentValidationService
{
    SubagentValidationResult Validate(SubagentDescriptor descriptor);
}
