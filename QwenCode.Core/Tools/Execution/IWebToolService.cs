using System.Text.Json;

using QwenCode.App.Models;

namespace QwenCode.App.Tools;

public interface IWebToolService
{
    Task<string> FetchAsync(
        QwenRuntimeProfile runtimeProfile,
        JsonElement arguments,
        CancellationToken cancellationToken = default);

    Task<string> SearchAsync(
        QwenRuntimeProfile runtimeProfile,
        JsonElement arguments,
        CancellationToken cancellationToken = default);
}
