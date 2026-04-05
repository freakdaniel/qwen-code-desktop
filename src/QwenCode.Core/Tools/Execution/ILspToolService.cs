using System.Text.Json;
using QwenCode.App.Compatibility;
using QwenCode.App.Models;

namespace QwenCode.App.Tools;

/// <summary>
/// Defines the contract for Lsp Tool Service
/// </summary>
public interface ILspToolService
{
    /// <summary>
    /// Executes async
    /// </summary>
    /// <param name="runtimeProfile">The runtime profile</param>
    /// <param name="arguments">The arguments</param>
    /// <param name="approvalState">The approval state</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that resolves to native tool execution result</returns>
    Task<NativeToolExecutionResult> ExecuteAsync(
        QwenRuntimeProfile runtimeProfile,
        JsonElement arguments,
        string approvalState,
        CancellationToken cancellationToken = default);
}
