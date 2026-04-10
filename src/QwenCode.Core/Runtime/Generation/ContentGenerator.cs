using System.Text.Json.Nodes;

namespace QwenCode.Core.Runtime;

/// <summary>
/// Represents the Content Generator
/// </summary>
/// <param name="llmClient">The llm client</param>
public sealed class ContentGenerator(IBaseLlmClient llmClient) : IContentGenerator
{
    /// <summary>
    /// Generates content async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that resolves to llm content response?</returns>
    public Task<LlmContentResponse?> GenerateContentAsync(
        LlmContentRequest request,
        CancellationToken cancellationToken = default) =>
        llmClient.GenerateContentAsync(request, cancellationToken);

    /// <summary>
    /// Generates json async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that resolves to json object?</returns>
    public Task<JsonObject?> GenerateJsonAsync(
        JsonGenerationRequest request,
        CancellationToken cancellationToken = default) =>
        llmClient.GenerateJsonAsync(request, cancellationToken);

    /// <summary>
    /// Generates embedding async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that resolves to embedding generation response?</returns>
    public Task<EmbeddingGenerationResponse?> GenerateEmbeddingAsync(
        EmbeddingGenerationRequest request,
        CancellationToken cancellationToken = default) =>
        llmClient.GenerateEmbeddingAsync(request, cancellationToken);
}
