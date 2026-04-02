using System.Text.Json;
using QwenCode.App.Models;

namespace QwenCode.App.Tools;

public interface ISkillToolService
{
    Task<string> LoadSkillContentAsync(
        QwenRuntimeProfile runtimeProfile,
        JsonElement arguments,
        CancellationToken cancellationToken = default);
}
