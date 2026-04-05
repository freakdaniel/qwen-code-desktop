using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using QwenCode.App.Models;
using QwenCode.App.Options;

namespace QwenCode.App.Runtime;

public sealed class OpenAiCompatibleBaseLlmClient(
    HttpClient httpClient,
    ProviderConfigurationResolver configurationResolver,
    IModelConfigResolver modelConfigResolver,
    ITokenLimitService tokenLimitService,
    IOptions<NativeAssistantRuntimeOptions> options) : IBaseLlmClient
{
    private readonly NativeAssistantRuntimeOptions runtimeOptions = options.Value;

    public async Task<LlmContentResponse?> GenerateContentAsync(
        LlmContentRequest request,
        CancellationToken cancellationToken = default)
    {
        var assistantRequest = ToAssistantTurnRequest(request);
        var configuration = configurationResolver.Resolve(assistantRequest, runtimeOptions);
        if (string.IsNullOrWhiteSpace(configuration.Endpoint) || string.IsNullOrWhiteSpace(configuration.ApiKey))
        {
            return null;
        }

        var tokenLimits = tokenLimitService.Resolve(configuration.Model, runtimeOptions);
        var payload = OpenAiCompatibleProtocol.BuildPayload(
            configuration.Model,
            request.TemperatureOverride ?? runtimeOptions.Temperature,
            tokenLimits.OutputTokenLimit,
            string.IsNullOrWhiteSpace(request.SystemPrompt) ? runtimeOptions.SystemPrompt : request.SystemPrompt,
            assistantRequest,
            request.PromptContext,
            [],
            request.Metadata,
            configuration.ExtraBody);
        payload["stream"] = false;

        using var httpRequest = BuildJsonRequest(configuration.Endpoint, configuration.ApiKey, configuration.Headers, payload);
        using var response = await httpClient.SendAsync(httpRequest, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var providerResponse = OpenAiCompatibleProtocol.TryReadResponse(body);
        if (providerResponse is null)
        {
            return null;
        }

        return new LlmContentResponse
        {
            ProviderName = configuration.IsDashScope ? "qwen-compatible" : "openai-compatible",
            Model = configuration.Model,
            Content = providerResponse.Summary,
            StopReason = "completed"
        };
    }

    public async Task<JsonObject?> GenerateJsonAsync(
        JsonGenerationRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await GenerateContentAsync(request.ContentRequest, cancellationToken);
        if (response is null || string.IsNullOrWhiteSpace(response.Content))
        {
            return null;
        }

        try
        {
            var jsonStart = response.Content.IndexOf('{');
            var jsonEnd = response.Content.LastIndexOf('}');
            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var jsonSlice = response.Content[jsonStart..(jsonEnd + 1)];
                return JsonNode.Parse(jsonSlice) as JsonObject;
            }

            return JsonNode.Parse(response.Content) as JsonObject;
        }
        catch
        {
            return null;
        }
    }

    public async Task<EmbeddingGenerationResponse?> GenerateEmbeddingAsync(
        EmbeddingGenerationRequest request,
        CancellationToken cancellationToken = default)
    {
        var assistantRequest = new AssistantTurnRequest
        {
            SessionId = request.SessionId,
            Prompt = string.Empty,
            WorkingDirectory = request.WorkingDirectory,
            TranscriptPath = string.Empty,
            RuntimeProfile = request.RuntimeProfile,
            ToolExecution = CreateNoOpToolExecution(request.WorkingDirectory),
            ModelOverride = request.ModelOverride,
            AuthTypeOverride = request.AuthTypeOverride,
            EndpointOverride = request.EndpointOverride,
            ApiKeyOverride = request.ApiKeyOverride,
            DisableTools = true
        };
        var configuration = configurationResolver.Resolve(assistantRequest, runtimeOptions);
        if (string.IsNullOrWhiteSpace(configuration.ApiKey))
        {
            return null;
        }

        var embeddingEndpoint = BuildEmbeddingsEndpoint(configuration.Endpoint, configuration.IsDashScope);
        var embeddingModel = modelConfigResolver.Resolve(
            new WorkspacePaths { WorkspaceRoot = request.RuntimeProfile.ProjectRoot },
            request.ModelOverride,
            request.AuthTypeOverride,
            embedding: true).Id;
        var payload = new JsonObject
        {
            ["model"] = embeddingModel,
            ["input"] = request.Input
        };

        using var httpRequest = BuildJsonRequest(embeddingEndpoint, configuration.ApiKey, configuration.Headers, payload);
        using var response = await httpClient.SendAsync(httpRequest, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        try
        {
            using var document = JsonDocument.Parse(body);
            if (!document.RootElement.TryGetProperty("data", out var data) ||
                data.ValueKind != JsonValueKind.Array ||
                data.GetArrayLength() == 0)
            {
                return null;
            }

            var first = data[0];
            if (!first.TryGetProperty("embedding", out var embeddingElement) ||
                embeddingElement.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            var vector = embeddingElement.EnumerateArray()
                .Select(static item => item.ValueKind == JsonValueKind.Number && item.TryGetSingle(out var value) ? value : 0f)
                .ToArray();

            return new EmbeddingGenerationResponse
            {
                ProviderName = configuration.IsDashScope ? "qwen-compatible" : "openai-compatible",
                Model = embeddingModel,
                Embedding = vector
            };
        }
        catch
        {
            return null;
        }
    }

    private static HttpRequestMessage BuildJsonRequest(
        string endpoint,
        string apiKey,
        IReadOnlyDictionary<string, string> headers,
        JsonObject payload)
    {
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint);
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        foreach (var header in headers)
        {
            httpRequest.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        httpRequest.Content = new StringContent(
            JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json");
        return httpRequest;
    }

    private static string BuildEmbeddingsEndpoint(string endpoint, bool isDashScope)
    {
        var normalized = endpoint.TrimEnd('/');
        if (normalized.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase))
        {
            return normalized[..^"/chat/completions".Length] + "/embeddings";
        }

        return isDashScope
            ? $"{OpenAiCompatibleProtocol.DefaultDashScopeBaseUrl}/embeddings"
            : $"{normalized}/embeddings";
    }

    private static AssistantTurnRequest ToAssistantTurnRequest(LlmContentRequest request) =>
        new()
        {
            SessionId = request.SessionId,
            Prompt = request.Prompt,
            WorkingDirectory = request.WorkingDirectory,
            TranscriptPath = request.TranscriptPath,
            RuntimeProfile = request.RuntimeProfile,
            GitBranch = request.GitBranch,
            ToolExecution = CreateNoOpToolExecution(request.WorkingDirectory),
            SystemPromptOverride = request.SystemPrompt,
            ModelOverride = request.ModelOverride,
            AuthTypeOverride = request.AuthTypeOverride,
            EndpointOverride = request.EndpointOverride,
            ApiKeyOverride = request.ApiKeyOverride,
            DisableTools = request.DisableTools
        };

    private static NativeToolExecutionResult CreateNoOpToolExecution(string workingDirectory) =>
        new()
        {
            ToolName = string.Empty,
            Status = "not-requested",
            ApprovalState = "allow",
            WorkingDirectory = workingDirectory,
            ChangedFiles = []
        };
}
