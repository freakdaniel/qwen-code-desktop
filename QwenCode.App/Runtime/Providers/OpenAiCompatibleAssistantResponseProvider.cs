using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using QwenCode.App.Models;
using QwenCode.App.Options;

namespace QwenCode.App.Runtime;

public sealed class OpenAiCompatibleAssistantResponseProvider(
    HttpClient httpClient,
    ProviderConfigurationResolver configurationResolver,
    ITokenLimitService tokenLimitService) : IAssistantResponseProvider
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

        var configuration = configurationResolver.Resolve(request, options);
        if (string.IsNullOrWhiteSpace(configuration.Endpoint) || string.IsNullOrWhiteSpace(configuration.ApiKey))
        {
            return null;
        }

        var tokenLimits = tokenLimitService.Resolve(configuration.Model, options);
        var payload = OpenAiCompatibleProtocol.BuildPayload(
            configuration.Model,
            options.Temperature,
            tokenLimits.OutputTokenLimit,
            string.IsNullOrWhiteSpace(request.SystemPromptOverride) ? options.SystemPrompt : request.SystemPromptOverride,
            request,
            promptContext,
            toolHistory,
            null,
            configuration.ExtraBody);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, configuration.Endpoint);
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", configuration.ApiKey);
        foreach (var header in configuration.Headers)
        {
            httpRequest.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

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
            Model = configuration.Model,
            ToolCalls = providerResponse.ToolCalls
        };
    }
}
