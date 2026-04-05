using System.Text.Json;
using QwenCode.App.Compatibility;
using QwenCode.App.Models;

namespace QwenCode.App.Tools;

public interface ILspToolService
{
    Task<NativeToolExecutionResult> ExecuteAsync(
        QwenRuntimeProfile runtimeProfile,
        JsonElement arguments,
        string approvalState,
        CancellationToken cancellationToken = default);
}
