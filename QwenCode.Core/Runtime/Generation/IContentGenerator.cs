using System.Text.Json.Nodes;

namespace QwenCode.App.Runtime;

public interface IContentGenerator
{
    Task<LlmContentResponse?> GenerateContentAsync(
        LlmContentRequest request,
        CancellationToken cancellationToken = default);

    Task<JsonObject?> GenerateJsonAsync(
        JsonGenerationRequest request,
        CancellationToken cancellationToken = default);

    Task<EmbeddingGenerationResponse?> GenerateEmbeddingAsync(
        EmbeddingGenerationRequest request,
        CancellationToken cancellationToken = default);
}
