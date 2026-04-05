using System.Text.Json.Nodes;

namespace QwenCode.Tests.Shared.Fakes;

internal sealed class StaticContentGenerator(
    Func<LlmContentRequest, LlmContentResponse?> contentResponder) : IContentGenerator
{
    public Task<LlmContentResponse?> GenerateContentAsync(
        LlmContentRequest request,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(contentResponder(request));

    public Task<JsonObject?> GenerateJsonAsync(
        JsonGenerationRequest request,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<JsonObject?>(null);

    public Task<EmbeddingGenerationResponse?> GenerateEmbeddingAsync(
        EmbeddingGenerationRequest request,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<EmbeddingGenerationResponse?>(null);
}
