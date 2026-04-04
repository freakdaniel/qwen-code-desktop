using System.Text.Json;
using System.Text.Json.Nodes;
using QwenCode.App.Auth;
using QwenCode.App.Config;
using QwenCode.App.Infrastructure;
using QwenCode.App.Options;
using QwenCode.App.Models;

namespace QwenCode.App.Runtime;

public sealed class ProviderConfigurationResolver(
    IDesktopEnvironmentPaths environmentPaths,
    IQwenOAuthCredentialStore? qwenOAuthCredentialStore = null,
    IQwenOAuthTokenManager? qwenOAuthTokenManager = null,
    IConfigService? configService = null,
    IModelConfigResolver? modelConfigResolver = null)
{
    private readonly IConfigService config = configService ?? new RuntimeConfigService(environmentPaths);
    private readonly IModelConfigResolver? _modelConfigResolver = modelConfigResolver;

    public ResolvedProviderConfiguration Resolve(
        AssistantTurnRequest request,
        NativeAssistantRuntimeOptions options)
    {
        var snapshot = config.Inspect(new WorkspacePaths { WorkspaceRoot = request.RuntimeProfile.ProjectRoot });
        var mergedSettings = snapshot.MergedSettings;
        var settingsEnvironment = snapshot.Environment;

        var authType = FirstNonEmpty(
            request.AuthTypeOverride,
            GetString(mergedSettings, "security", "auth", "selectedType"));
        if (string.IsNullOrWhiteSpace(authType))
        {
            authType = "openai";
        }

        var configuredModel = FirstNonEmpty(
            request.ModelOverride,
            options.Model,
            Environment.GetEnvironmentVariable("QWENCODE_ASSISTANT_MODEL"),
            ReadEnvironmentValue(settingsEnvironment, "OPENAI_MODEL", "QWEN_MODEL"),
            Environment.GetEnvironmentVariable("OPENAI_MODEL"),
            Environment.GetEnvironmentVariable("QWEN_MODEL"),
            GetString(mergedSettings, "model", "name"));

        var resolvedModelMetadata = _modelConfigResolver?.Resolve(
            new WorkspacePaths { WorkspaceRoot = request.RuntimeProfile.ProjectRoot },
            configuredModel,
            authType);
        authType = FirstNonEmpty(resolvedModelMetadata?.AuthType, authType);

        var modelProvider = FindModelProvider(mergedSettings, authType, configuredModel);
        var resolvedModel = FirstNonEmpty(configuredModel, resolvedModelMetadata?.Id, modelProvider?.Id, "qwen3-coder-plus");
        modelProvider ??= FindModelProvider(mergedSettings, authType, resolvedModel);

        var apiKeyEnvironmentVariable = FirstNonEmpty(
            modelProvider?.EnvironmentVariableName,
            resolvedModelMetadata?.ApiKeyEnvironmentVariable,
            options.ApiKeyEnvironmentVariable,
            ResolveDefaultApiKeyEnvironmentVariable(authType));
        var apiKey = ResolveApiKey(
            request.ApiKeyOverride,
            options,
            settingsEnvironment,
            apiKeyEnvironmentVariable,
            GetString(mergedSettings, "security", "auth", "apiKey"),
            modelProvider?.HasExplicitEnvironmentVariable == true,
            authType,
            qwenOAuthCredentialStore,
            qwenOAuthTokenManager);

        var baseUrl = FirstNonEmpty(
            request.EndpointOverride,
            options.Endpoint,
            Environment.GetEnvironmentVariable("QWENCODE_ASSISTANT_ENDPOINT"),
            modelProvider?.BaseUrl,
            resolvedModelMetadata?.BaseUrl,
            ReadEnvironmentValue(settingsEnvironment, "OPENAI_BASE_URL", "QWEN_BASE_URL"),
            Environment.GetEnvironmentVariable("OPENAI_BASE_URL"),
            Environment.GetEnvironmentVariable("QWEN_BASE_URL"),
            GetString(mergedSettings, "security", "auth", "baseUrl"),
            OpenAiCompatibleProtocol.DefaultDashScopeBaseUrl);
        var endpoint = OpenAiCompatibleProtocol.EnsureChatCompletionsEndpoint(baseUrl);
        var isDashScope = OpenAiCompatibleProtocol.IsDashScopeEndpoint(baseUrl) ||
                          OpenAiCompatibleProtocol.IsDashScopeEndpoint(endpoint);
        var settingsCustomHeaders = ReadDictionary(mergedSettings, "model", "generationConfig", "customHeaders");
        var settingsExtraBody = ReadObject(mergedSettings, "model", "generationConfig", "extra_body");
        if (modelProvider?.ExtraBody is { Count: > 0 })
        {
            OpenAiCompatibleProtocol.MergeObjects(settingsExtraBody, modelProvider.ExtraBody);
        }

        return new ResolvedProviderConfiguration
        {
            AuthType = authType,
            Model = resolvedModel,
            Endpoint = endpoint,
            ApiKey = apiKey,
            ApiKeyEnvironmentVariable = apiKeyEnvironmentVariable,
            Headers = BuildHeaders(authType, isDashScope, settingsCustomHeaders, modelProvider?.CustomHeaders),
            ExtraBody = settingsExtraBody,
            IsDashScope = isDashScope
        };
    }

    private static IReadOnlyDictionary<string, string> BuildHeaders(
        string authType,
        bool isDashScope,
        IReadOnlyDictionary<string, string>? settingsHeaders,
        IReadOnlyDictionary<string, string>? customHeaders)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (isDashScope)
        {
            var userAgent = OpenAiCompatibleProtocol.BuildUserAgent();
            headers["User-Agent"] = userAgent;
            headers["X-DashScope-CacheControl"] = "enable";
            headers["X-DashScope-UserAgent"] = userAgent;
            headers["X-DashScope-AuthType"] = authType;
        }

        if (settingsHeaders is not null)
        {
            foreach (var header in settingsHeaders)
            {
                headers[header.Key] = header.Value;
            }
        }

        if (customHeaders is not null)
        {
            foreach (var header in customHeaders)
            {
                headers[header.Key] = header.Value;
            }
        }

        return headers;
    }

    private static string ResolveApiKey(
        string requestApiKey,
        NativeAssistantRuntimeOptions options,
        IReadOnlyDictionary<string, string> settingsEnvironment,
        string apiKeyEnvironmentVariable,
        string settingsApiKey,
        bool explicitEnvironmentVariableRequired,
        string authType,
        IQwenOAuthCredentialStore? qwenOAuthCredentialStore,
        IQwenOAuthTokenManager? qwenOAuthTokenManager)
    {
        if (!string.IsNullOrWhiteSpace(requestApiKey))
        {
            return requestApiKey;
        }

        if (!string.IsNullOrWhiteSpace(options.ApiKey))
        {
            return options.ApiKey;
        }

        if (!string.IsNullOrWhiteSpace(apiKeyEnvironmentVariable))
        {
            var environmentApiKey = FirstNonEmpty(
                Environment.GetEnvironmentVariable(apiKeyEnvironmentVariable),
                ReadEnvironmentValue(settingsEnvironment, apiKeyEnvironmentVariable));
            if (!string.IsNullOrWhiteSpace(environmentApiKey))
            {
                return environmentApiKey;
            }
        }

        if (string.Equals(authType, "qwen-oauth", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(authType, "qwen_oauth", StringComparison.OrdinalIgnoreCase))
        {
            var persistedAccessToken = qwenOAuthTokenManager?
                                           .GetValidCredentialsAsync()
                                           .GetAwaiter()
                                           .GetResult()?
                                           .AccessToken ??
                                       qwenOAuthCredentialStore?
                                           .ReadAsync()
                                           .GetAwaiter()
                                           .GetResult()?
                                           .AccessToken;
            if (!string.IsNullOrWhiteSpace(persistedAccessToken))
            {
                return persistedAccessToken;
            }
        }

        if (explicitEnvironmentVariableRequired)
        {
            return string.Empty;
        }

        return settingsApiKey;
    }

    private static string ResolveDefaultApiKeyEnvironmentVariable(string authType) =>
        authType switch
        {
            "qwen-oauth" => "QWEN_OAUTH_ACCESS_TOKEN",
            "qwen_oauth" => "QWEN_OAUTH_ACCESS_TOKEN",
            _ => "OPENAI_API_KEY"
        };

    private static string ReadEnvironmentValue(
        IReadOnlyDictionary<string, string> settingsEnvironment,
        params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!string.IsNullOrWhiteSpace(key) &&
                settingsEnvironment.TryGetValue(key, out var value) &&
                !string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return string.Empty;
    }

    private static string FirstNonEmpty(params string?[] candidates) =>
        candidates.FirstOrDefault(static candidate => !string.IsNullOrWhiteSpace(candidate)) ?? string.Empty;

    private static string GetString(JsonObject root, params string[] path) =>
        GetNode(root, path) is JsonValue value && value.TryGetValue<string>(out var result)
            ? result ?? string.Empty
            : string.Empty;

    private static JsonNode? GetNode(JsonObject root, params string[] path)
    {
        JsonNode? current = root;
        foreach (var segment in path)
        {
            if (current is not JsonObject currentObject ||
                !currentObject.TryGetPropertyValue(segment, out current) ||
                current is null)
            {
                return null;
            }
        }

        return current;
    }

    private static ProviderModelConfiguration? FindModelProvider(
        JsonObject mergedSettings,
        string authType,
        string modelId)
    {
        if (string.IsNullOrWhiteSpace(authType) || string.IsNullOrWhiteSpace(modelId))
        {
            return null;
        }

        if (GetNode(mergedSettings, "modelProviders", authType) is not JsonArray providers)
        {
            return null;
        }

        foreach (var providerNode in providers)
        {
            if (providerNode is not JsonObject providerObject)
            {
                continue;
            }

            var id = GetString(providerObject, "id");
            if (!string.Equals(id, modelId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return new ProviderModelConfiguration
            {
                Id = id,
                EnvironmentVariableName = GetString(providerObject, "envKey"),
                HasExplicitEnvironmentVariable = !string.IsNullOrWhiteSpace(GetString(providerObject, "envKey")),
                BaseUrl = GetString(providerObject, "baseUrl"),
                CustomHeaders = ReadDictionary(providerObject, "generationConfig", "customHeaders"),
                ExtraBody = ReadObject(providerObject, "generationConfig", "extra_body")
            };
        }

        return null;
    }

    private static IReadOnlyDictionary<string, string> ReadDictionary(JsonObject root, params string[] path)
    {
        if (GetNode(root, path) is not JsonObject objectNode)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        return objectNode
            .Where(static pair => pair.Value is JsonValue)
            .Select(pair => new KeyValuePair<string, string?>(
                pair.Key,
                pair.Value?.GetValue<string>()))
            .Where(static pair => !string.IsNullOrWhiteSpace(pair.Value))
            .ToDictionary(
                static pair => pair.Key,
                static pair => pair.Value!,
                StringComparer.OrdinalIgnoreCase);
    }

    private static JsonObject ReadObject(JsonObject root, params string[] path) =>
        GetNode(root, path) is JsonObject objectNode
            ? (JsonObject)objectNode.DeepClone()
            : new JsonObject();

    private sealed class ProviderModelConfiguration
    {
        public required string Id { get; init; }

        public required string EnvironmentVariableName { get; init; }

        public required bool HasExplicitEnvironmentVariable { get; init; }

        public required string BaseUrl { get; init; }

        public required IReadOnlyDictionary<string, string> CustomHeaders { get; init; }

        public required JsonObject ExtraBody { get; init; }
    }
}
