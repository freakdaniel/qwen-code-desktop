using System.Text.Json.Nodes;

namespace QwenCode.App.Runtime;

public sealed class ContentGenerator(IBaseLlmClient llmClient) : IContentGenerator
{
    public Task<LlmContentResponse?> GenerateContentAsync(
        LlmContentRequest request,
        CancellationToken cancellationToken = default) =>
        llmClient.GenerateContentAsync(request, cancellationToken);

    public Task<JsonObject?> GenerateJsonAsync(
        JsonGenerationRequest request,
        CancellationToken cancellationToken = default) =>
        llmClient.GenerateJsonAsync(request, cancellationToken);

    public Task<EmbeddingGenerationResponse?> GenerateEmbeddingAsync(
        EmbeddingGenerationRequest request,
        CancellationToken cancellationToken = default) =>
        llmClient.GenerateEmbeddingAsync(request, cancellationToken);
}
