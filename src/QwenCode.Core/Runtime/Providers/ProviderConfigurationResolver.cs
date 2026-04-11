using QwenCode.Core.Auth;
using QwenCode.Core.Config;
using QwenCode.Core.Infrastructure;
using QwenCode.Core.Models;

namespace QwenCode.Core.Runtime;

/// <summary>
/// Represents the Provider Configuration Resolver
/// </summary>
/// <param name="environmentPaths">The environment paths</param>
/// <param name="qwenOAuthCredentialStore">The qwen o auth credential store</param>
/// <param name="qwenOAuthTokenManager">The qwen o auth token manager</param>
/// <param name="configService">The config service</param>
/// <param name="modelConfigResolver">The model config resolver</param>
public sealed class ProviderConfigurationResolver(
    IDesktopEnvironmentPaths environmentPaths,
    IQwenOAuthCredentialStore? qwenOAuthCredentialStore = null,
    IQwenOAuthTokenManager? qwenOAuthTokenManager = null,
    IConfigService? configService = null,
    IModelConfigResolver? modelConfigResolver = null)
{
    internal const string OpenRouterBaseUrl = "https://openrouter.ai/api/v1";
    internal const string DeepSeekBaseUrl = "https://api.deepseek.com/v1";
    internal const string ModelScopeBaseUrl = "https://api.modelscope.cn/v1";
    internal const string DefaultQwenOAuthModel = "coder-model";
    private static readonly HashSet<string> QwenOAuthAllowedModels =
        [DefaultQwenOAuthModel];
    private readonly IConfigService config = configService ?? new RuntimeConfigService(environmentPaths);
    private readonly IModelConfigResolver? _modelConfigResolver = modelConfigResolver;

    /// <summary>
    /// Resolves value
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <param name="options">The options</param>
    /// <returns>The resulting resolved provider configuration</returns>
    public ResolvedProviderConfiguration Resolve(
        AssistantTurnRequest request,
        NativeAssistantRuntimeOptions options)
    {
        var snapshot = config.Inspect(new WorkspacePaths { WorkspaceRoot = request.RuntimeProfile.ProjectRoot });
        var mergedSettings = snapshot.MergedSettings;
        var settingsEnvironment = snapshot.Environment;
        var persistedQwenCredentials = ResolveQwenOAuthCredentials(qwenOAuthCredentialStore, qwenOAuthTokenManager);

        var authType = FirstNonEmpty(
            request.AuthTypeOverride,
            GetString(mergedSettings, "security", "auth", "selectedType"));
        if (string.IsNullOrWhiteSpace(authType))
        {
            authType = "openai";
        }

        var configuredModel = ResolveConfiguredModel(
            authType,
            request,
            options,
            settingsEnvironment,
            mergedSettings);

        var resolvedModelMetadata = _modelConfigResolver?.Resolve(
            new WorkspacePaths { WorkspaceRoot = request.RuntimeProfile.ProjectRoot },
            configuredModel,
            authType);
        authType = FirstNonEmpty(resolvedModelMetadata?.AuthType, authType);

        var modelProvider = FindModelProvider(mergedSettings, authType, configuredModel);
        var resolvedModel = FirstNonEmpty(
            resolvedModelMetadata?.Id,
            configuredModel,
            modelProvider?.Id,
            ResolveDefaultModel(authType));
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
            persistedQwenCredentials);

        var baseUrl = FirstNonEmpty(
            request.EndpointOverride,
            options.Endpoint,
            Environment.GetEnvironmentVariable("QWENCODE_ASSISTANT_ENDPOINT"),
            ResolveQwenOAuthBaseUrl(authType, persistedQwenCredentials),
            modelProvider?.BaseUrl,
            resolvedModelMetadata?.BaseUrl,
            ReadEnvironmentValue(settingsEnvironment, "OPENAI_BASE_URL", "QWEN_BASE_URL"),
            Environment.GetEnvironmentVariable("OPENAI_BASE_URL"),
            Environment.GetEnvironmentVariable("QWEN_BASE_URL"),
            GetString(mergedSettings, "security", "auth", "baseUrl"),
            ResolveDefaultBaseUrl(authType));
        var endpoint = OpenAiCompatibleProtocol.EnsureChatCompletionsEndpoint(baseUrl);
        var isDashScope = OpenAiCompatibleProtocol.IsDashScopeEndpoint(baseUrl) ||
                          OpenAiCompatibleProtocol.IsDashScopeEndpoint(endpoint);
        var providerFlavor = ResolveProviderFlavor(authType, baseUrl, endpoint);
        var settingsCustomHeaders = ReadDictionary(mergedSettings, "model", "generationConfig", "customHeaders");
        var settingsExtraBody = ReadObject(mergedSettings, "model", "generationConfig", "extra_body");
        if (modelProvider?.ExtraBody is { Count: > 0 })
        {
            OpenAiCompatibleProtocol.MergeObjects(settingsExtraBody, modelProvider.ExtraBody);
        }

        return new ResolvedProviderConfiguration
        {
            AuthType = authType,
            ProviderFlavor = providerFlavor,
            Model = resolvedModel,
            Endpoint = endpoint,
            ApiKey = apiKey,
            ApiKeyEnvironmentVariable = apiKeyEnvironmentVariable,
            Headers = BuildHeaders(authType, providerFlavor, isDashScope, settingsCustomHeaders, modelProvider?.CustomHeaders),
            ExtraBody = settingsExtraBody,
            IsDashScope = isDashScope
        };
    }

    private static IReadOnlyDictionary<string, string> BuildHeaders(
        string authType,
        string providerFlavor,
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

        if (string.Equals(providerFlavor, "openrouter", StringComparison.OrdinalIgnoreCase))
        {
            headers["HTTP-Referer"] = "https://github.com/QwenLM/qwen-code.git";
            headers["X-OpenRouter-Title"] = "Qwen Code";
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
        QwenOAuthCredentials? persistedQwenCredentials)
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
            var persistedAccessToken = persistedQwenCredentials?.AccessToken;
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
            "openrouter" => "OPENROUTER_API_KEY",
            "deepseek" => "DEEPSEEK_API_KEY",
            "modelscope" => "MODELSCOPE_API_KEY",
            _ => "OPENAI_API_KEY"
        };

    private static string ResolveDefaultBaseUrl(string authType) =>
        authType switch
        {
            "openrouter" => OpenRouterBaseUrl,
            "deepseek" => DeepSeekBaseUrl,
            "modelscope" => ModelScopeBaseUrl,
            _ => OpenAiCompatibleProtocol.DefaultDashScopeBaseUrl
        };

    private static string ResolveDefaultModel(string authType) =>
        string.Equals(authType, "qwen-oauth", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(authType, "qwen_oauth", StringComparison.OrdinalIgnoreCase)
            ? DefaultQwenOAuthModel
            : "qwen3-coder-plus";

    private static string ResolveConfiguredModel(
        string authType,
        AssistantTurnRequest request,
        NativeAssistantRuntimeOptions options,
        IReadOnlyDictionary<string, string> settingsEnvironment,
        JsonObject mergedSettings)
    {
        var configuredModel = string.Equals(authType, "qwen-oauth", StringComparison.OrdinalIgnoreCase) ||
                              string.Equals(authType, "qwen_oauth", StringComparison.OrdinalIgnoreCase)
            ? FirstNonEmpty(
                request.ModelOverride,
                options.Model,
                Environment.GetEnvironmentVariable("QWENCODE_ASSISTANT_MODEL"),
                GetString(mergedSettings, "model", "name"))
            : FirstNonEmpty(
                request.ModelOverride,
                options.Model,
                Environment.GetEnvironmentVariable("QWENCODE_ASSISTANT_MODEL"),
                ReadEnvironmentValue(settingsEnvironment, "OPENAI_MODEL", "QWEN_MODEL"),
                Environment.GetEnvironmentVariable("OPENAI_MODEL"),
                Environment.GetEnvironmentVariable("QWEN_MODEL"),
                GetString(mergedSettings, "model", "name"));

        if ((string.Equals(authType, "qwen-oauth", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(authType, "qwen_oauth", StringComparison.OrdinalIgnoreCase)) &&
            !string.IsNullOrWhiteSpace(configuredModel) &&
            !QwenOAuthAllowedModels.Contains(configuredModel))
        {
            return string.Empty;
        }

        return configuredModel;
    }

    private static string ResolveProviderFlavor(string authType, string baseUrl, string endpoint)
    {
        if (string.Equals(authType, "openrouter", StringComparison.OrdinalIgnoreCase) ||
            OpenAiCompatibleProtocol.IsOpenRouterEndpoint(baseUrl) ||
            OpenAiCompatibleProtocol.IsOpenRouterEndpoint(endpoint))
        {
            return "openrouter";
        }

        if (string.Equals(authType, "deepseek", StringComparison.OrdinalIgnoreCase) ||
            OpenAiCompatibleProtocol.IsDeepSeekEndpoint(baseUrl) ||
            OpenAiCompatibleProtocol.IsDeepSeekEndpoint(endpoint))
        {
            return "deepseek";
        }

        if (string.Equals(authType, "modelscope", StringComparison.OrdinalIgnoreCase) ||
            OpenAiCompatibleProtocol.IsModelScopeEndpoint(baseUrl) ||
            OpenAiCompatibleProtocol.IsModelScopeEndpoint(endpoint))
        {
            return "modelscope";
        }

        if (OpenAiCompatibleProtocol.IsDashScopeEndpoint(baseUrl) ||
            OpenAiCompatibleProtocol.IsDashScopeEndpoint(endpoint))
        {
            return "dashscope";
        }

        return "openai-compatible";
    }

    private static QwenOAuthCredentials? ResolveQwenOAuthCredentials(
        IQwenOAuthCredentialStore? qwenOAuthCredentialStore,
        IQwenOAuthTokenManager? qwenOAuthTokenManager)
    {
        return qwenOAuthTokenManager?
                   .GetValidCredentialsAsync()
                   .GetAwaiter()
                   .GetResult() ??
               qwenOAuthCredentialStore?
                   .ReadAsync()
                   .GetAwaiter()
                   .GetResult();
    }

    private static string ResolveQwenOAuthBaseUrl(string authType, QwenOAuthCredentials? persistedQwenCredentials)
    {
        if (!string.Equals(authType, "qwen-oauth", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(authType, "qwen_oauth", StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        if (string.IsNullOrWhiteSpace(persistedQwenCredentials?.ResourceUrl))
        {
            return string.Empty;
        }

        var resourceUrl = persistedQwenCredentials.ResourceUrl.Trim();
        var normalizedUrl = resourceUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? resourceUrl
            : $"https://{resourceUrl}";

        return normalizedUrl.EndsWith("/v1", StringComparison.OrdinalIgnoreCase)
            ? normalizedUrl
            : $"{normalizedUrl.TrimEnd('/')}/v1";
    }

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
        /// <summary>
        /// Gets or sets the id
        /// </summary>
        public required string Id { get; init; }

        /// <summary>
        /// Gets or sets the environment variable name
        /// </summary>
        public required string EnvironmentVariableName { get; init; }

        /// <summary>
        /// Gets or sets a value indicating whether has explicit environment variable
        /// </summary>
        public required bool HasExplicitEnvironmentVariable { get; init; }

        /// <summary>
        /// Gets or sets the base url
        /// </summary>
        public required string BaseUrl { get; init; }

        /// <summary>
        /// Gets or sets the custom headers
        /// </summary>
        public required IReadOnlyDictionary<string, string> CustomHeaders { get; init; }

        /// <summary>
        /// Gets or sets the extra body
        /// </summary>
        public required JsonObject ExtraBody { get; init; }
    }
}
