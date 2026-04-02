using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using QwenCode.App.Options;
using QwenCode.App.Models;

namespace QwenCode.App.Runtime;

public sealed class OpenAiCompatibleAssistantResponseProvider(HttpClient httpClient) : IAssistantResponseProvider
{
    public string Name => "openai-compatible";

    public async Task<AssistantTurnResponse?> TryGenerateAsync(
        AssistantTurnRequest request,
        AssistantPromptContext promptContext,
        IReadOnlyList<AssistantToolCallResult> toolHistory,
        NativeAssistantRuntimeOptions options,
        Action<AssistantRuntimeEvent>? eventSink = null,
        CancellationToken cancellationToken = default)
    {
        if (!string.Equals(options.Provider, Name, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var endpoint = ResolveEndpoint(options);
        var apiKey = ResolveApiKey(options);
        if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(apiKey))
        {
            return null;
        }

        var resolvedModel = string.IsNullOrWhiteSpace(options.Model) ? "qwen3-coder-plus" : options.Model;
        var payload = OpenAiCompatibleProtocol.BuildPayload(
            resolvedModel,
            options.Temperature,
            options.SystemPrompt,
            request,
            promptContext,
            toolHistory);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint);
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        httpRequest.Content = new StringContent(
            JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json");

        using var response = await httpClient.SendAsync(httpRequest, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var providerResponse = OpenAiCompatibleProtocol.TryReadResponse(content);
        if (providerResponse is null)
        {
            return null;
        }

        return new AssistantTurnResponse
        {
            Summary = providerResponse.Summary,
            ProviderName = Name,
            Model = resolvedModel,
            ToolCalls = providerResponse.ToolCalls
        };
    }

    private static string ResolveEndpoint(NativeAssistantRuntimeOptions options) =>
        !string.IsNullOrWhiteSpace(options.Endpoint)
            ? options.Endpoint
            : Environment.GetEnvironmentVariable("QWENCODE_ASSISTANT_ENDPOINT") ?? string.Empty;

    private static string ResolveApiKey(NativeAssistantRuntimeOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.ApiKey))
        {
            return options.ApiKey;
        }

        var environmentVariable = string.IsNullOrWhiteSpace(options.ApiKeyEnvironmentVariable)
            ? "OPENAI_API_KEY"
            : options.ApiKeyEnvironmentVariable;
        return Environment.GetEnvironmentVariable(environmentVariable) ?? string.Empty;
    }
}
