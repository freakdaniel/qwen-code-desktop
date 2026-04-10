using System.Text.Json.Nodes;

namespace QwenCode.Core.Runtime;

/// <summary>
/// Defines the contract for Base Llm Client
/// </summary>
public interface IBaseLlmClient
{
    /// <summary>
    /// Generates content async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that resolves to llm content response?</returns>
    Task<LlmContentResponse?> GenerateContentAsync(
        LlmContentRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates json async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that resolves to json object?</returns>
    Task<JsonObject?> GenerateJsonAsync(
        JsonGenerationRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates embedding async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that resolves to embedding generation response?</returns>
    Task<EmbeddingGenerationResponse?> GenerateEmbeddingAsync(
        EmbeddingGenerationRequest request,
        CancellationToken cancellationToken = default);
}
