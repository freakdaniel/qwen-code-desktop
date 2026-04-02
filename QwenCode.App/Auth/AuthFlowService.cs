using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using QwenCode.App.Compatibility;
using QwenCode.App.Infrastructure;
using QwenCode.App.Models;

namespace QwenCode.App.Auth;

public sealed class AuthFlowService(
    QwenRuntimeProfileService runtimeProfileService,
    IDesktopEnvironmentPaths environmentPaths,
    IQwenOAuthCredentialStore qwenOAuthCredentialStore,
    HttpClient httpClient,
    IAuthUrlLauncher authUrlLauncher,
    IQwenOAuthTokenManager? qwenOAuthTokenManager = null) : IAuthFlowService
{
    private const string CodingPlanEnvKey = "BAILIAN_CODING_PLAN_API_KEY";
    private const string QwenOAuthBaseUrl = "https://chat.qwen.ai";
    private const string QwenOAuthDeviceCodeEndpoint = $"{QwenOAuthBaseUrl}/api/v1/oauth2/device/code";
    private const string QwenOAuthTokenEndpoint = $"{QwenOAuthBaseUrl}/api/v1/oauth2/token";
    private const string QwenOAuthClientId = "f0304373b74a44d2b584a3fb70ca9e56";
    private const string QwenOAuthScope = "openid profile email model.completion";
    private const string QwenOAuthGrantType = "urn:ietf:params:oauth:grant-type:device_code";
    private readonly Lock lifecycleSync = new();
    private readonly Dictionary<string, DeviceFlowState> deviceFlows = new(StringComparer.Ordinal);
    private readonly IQwenOAuthTokenManager tokenManager = qwenOAuthTokenManager ??
                                                            new QwenOAuthTokenManager(
                                                                qwenOAuthCredentialStore,
                                                                environmentPaths,
                                                                httpClient);

    public event EventHandler<AuthStatusSnapshot>? AuthChanged;

    public AuthStatusSnapshot GetStatus(WorkspacePaths paths)
    {
        var runtimeProfile = runtimeProfileService.Inspect(paths);
        var mergedSettings = LoadMergedSettings(runtimeProfile);
        var qwenCredentials = tokenManager.GetValidCredentialsAsync().GetAwaiter().GetResult();

        var selectedType = FirstNonEmpty(GetString(mergedSettings, "security", "auth", "selectedType"), "openai");
        var selectedScope = ResolveSelectedScope(runtimeProfile);
        var model = GetString(mergedSettings, "model", "name");
        var endpoint = ResolveEndpoint(mergedSettings, selectedType, model);
        var apiKeyEnvironmentVariable = ResolveApiKeyEnvironmentVariable(mergedSettings, selectedType, model);
        var hasApiKey = ResolveHasApiKey(mergedSettings, selectedType, apiKeyEnvironmentVariable, qwenCredentials);

        return new AuthStatusSnapshot
        {
            SelectedType = selectedType,
            SelectedScope = selectedScope,
            DisplayName = ResolveDisplayName(mergedSettings, selectedType),
            Status = hasApiKey ? "connected" : "missing-credentials",
            Model = model,
            Endpoint = endpoint,
            ApiKeyEnvironmentVariable = apiKeyEnvironmentVariable,
            HasApiKey = hasApiKey,
            HasQwenOAuthCredentials = !string.IsNullOrWhiteSpace(qwenCredentials?.AccessToken),
            HasRefreshToken = !string.IsNullOrWhiteSpace(qwenCredentials?.RefreshToken),
            CredentialPath = qwenOAuthCredentialStore.CredentialPath,
            LastError = hasApiKey
                ? string.Empty
                : ResolveMissingCredentialMessage(selectedType, apiKeyEnvironmentVariable),
            LastAuthenticatedAtUtc = File.Exists(qwenOAuthCredentialStore.CredentialPath)
                ? File.GetLastWriteTimeUtc(qwenOAuthCredentialStore.CredentialPath)
                : null,
            DeviceFlow = GetDeviceFlowSnapshot(runtimeProfile.ProjectRoot)
        };
    }

    public async Task<AuthStatusSnapshot> ConfigureOpenAiCompatibleAsync(
        WorkspacePaths paths,
        ConfigureOpenAiCompatibleAuthRequest request,
        CancellationToken cancellationToken = default)
    {
        var runtimeProfile = runtimeProfileService.Inspect(paths);
        var settingsPath = ResolveSettingsPath(runtimeProfile, request.Scope);
        var root = LoadSettingsRoot(settingsPath);
        var authType = string.IsNullOrWhiteSpace(request.AuthType) ? "openai" : request.AuthType.Trim();

        SetValue(root, "security", "auth", "selectedType", authType);
        SetValue(root, "security", "auth", "baseUrl", request.BaseUrl);
        SetValue(root, "model", "name", request.Model);

        if (!string.IsNullOrWhiteSpace(request.ApiKey))
        {
            if (!string.IsNullOrWhiteSpace(request.ApiKeyEnvironmentVariable))
            {
                SetValue(root, "env", request.ApiKeyEnvironmentVariable, request.ApiKey);
                RemoveValue(root, "security", "auth", "apiKey");
            }
            else
            {
                SetValue(root, "security", "auth", "apiKey", request.ApiKey);
            }
        }

        UpsertModelProvider(root, authType, request.Model, request.BaseUrl, request.ApiKeyEnvironmentVariable);
        SaveSettingsRoot(settingsPath, root);
        return PublishStatus(paths);
    }

    public Task<AuthStatusSnapshot> ConfigureCodingPlanAsync(
        WorkspacePaths paths,
        ConfigureCodingPlanAuthRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.ApiKey))
        {
            throw new InvalidOperationException("Coding Plan API key is required.");
        }

        var runtimeProfile = runtimeProfileService.Inspect(paths);
        var settingsPath = ResolveSettingsPath(runtimeProfile, request.Scope);
        var root = LoadSettingsRoot(settingsPath);
        var region = string.Equals(request.Region, "global", StringComparison.OrdinalIgnoreCase) ? "global" : "china";
        var template = CreateCodingPlanTemplate(region);
        var preferredModel = !string.IsNullOrWhiteSpace(request.Model) &&
                             template.Any(item => string.Equals(item.Id, request.Model, StringComparison.OrdinalIgnoreCase))
            ? request.Model
            : template[0].Id;

        SetValue(root, "security", "auth", "selectedType", "openai");
        SetValue(root, "env", CodingPlanEnvKey, request.ApiKey);
        SetValue(root, "codingPlan", "region", region);
        SetValue(root, "codingPlan", "version", ComputeTemplateVersion(template));
        SetValue(root, "model", "name", preferredModel);
        ReplaceCodingPlanProviders(root, template);

        SaveSettingsRoot(settingsPath, root);
        return Task.FromResult(PublishStatus(paths));
    }

    public async Task<AuthStatusSnapshot> ConfigureQwenOAuthAsync(
        WorkspacePaths paths,
        ConfigureQwenOAuthRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.AccessToken))
        {
            throw new InvalidOperationException("Qwen OAuth access token is required.");
        }

        var runtimeProfile = runtimeProfileService.Inspect(paths);
        var settingsPath = ResolveSettingsPath(runtimeProfile, request.Scope);
        var root = LoadSettingsRoot(settingsPath);
        SetValue(root, "security", "auth", "selectedType", "qwen-oauth");
        RemoveValue(root, "security", "auth", "apiKey");
        SaveSettingsRoot(settingsPath, root);

        await tokenManager.StoreCredentialsAsync(
            new QwenOAuthCredentials
            {
                AccessToken = request.AccessToken,
                RefreshToken = request.RefreshToken,
                IdToken = request.IdToken,
                TokenType = request.TokenType,
                ResourceUrl = request.ResourceUrl,
                ExpiresAtUtc = request.ExpiresAtUtc
            },
            cancellationToken);

        return PublishStatus(paths);
    }

    public async Task<AuthStatusSnapshot> StartQwenOAuthDeviceFlowAsync(
        WorkspacePaths paths,
        StartQwenOAuthDeviceFlowRequest request,
        CancellationToken cancellationToken = default)
    {
        var runtimeProfile = runtimeProfileService.Inspect(paths);
        var pkce = GeneratePkcePair();
        var authorization = await RequestDeviceAuthorizationAsync(pkce.CodeChallenge, cancellationToken);
        var browserLaunchSucceeded = await authUrlLauncher.LaunchAsync(authorization.VerificationUriComplete, cancellationToken);

        var state = new DeviceFlowState
        {
            FlowId = Guid.NewGuid().ToString("N"),
            Scope = string.Equals(request.Scope, "project", StringComparison.OrdinalIgnoreCase) ? "project" : "user",
            ProjectRoot = runtimeProfile.ProjectRoot,
            DeviceCode = authorization.DeviceCode,
            CodeVerifier = pkce.CodeVerifier,
            VerificationUri = authorization.VerificationUri,
            VerificationUriComplete = authorization.VerificationUriComplete,
            UserCode = authorization.UserCode,
            StartedAtUtc = DateTimeOffset.UtcNow,
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddSeconds(authorization.ExpiresIn),
            PollIntervalMs = 2000,
            BrowserLaunchAttempted = true,
            BrowserLaunchSucceeded = browserLaunchSucceeded,
            Status = "pending",
            Cancellation = new CancellationTokenSource()
        };

        lock (lifecycleSync)
        {
            foreach (var existing in deviceFlows.Values.Where(flow => string.Equals(flow.ProjectRoot, runtimeProfile.ProjectRoot, StringComparison.OrdinalIgnoreCase)).ToArray())
            {
                existing.Cancellation.Cancel();
            }

            deviceFlows[state.FlowId] = state;
        }

        _ = Task.Run(() => PollDeviceFlowAsync(paths, state), CancellationToken.None);
        return PublishStatus(paths);
    }

    public Task<AuthStatusSnapshot> CancelQwenOAuthDeviceFlowAsync(
        WorkspacePaths paths,
        CancelQwenOAuthDeviceFlowRequest request,
        CancellationToken cancellationToken = default)
    {
        lock (lifecycleSync)
        {
            var target = ResolveDeviceFlow(paths, request.FlowId);
            if (target is not null)
            {
                target.Status = "cancelled";
                target.ErrorMessage = "Authentication cancelled by user.";
                target.CompletedAtUtc = DateTimeOffset.UtcNow;
                target.Cancellation.Cancel();
            }
        }

        return Task.FromResult(PublishStatus(paths));
    }

    public async Task<AuthStatusSnapshot> DisconnectAsync(
        WorkspacePaths paths,
        DisconnectAuthRequest request,
        CancellationToken cancellationToken = default)
    {
        var runtimeProfile = runtimeProfileService.Inspect(paths);
        var settingsPath = ResolveSettingsPath(runtimeProfile, request.Scope);
        var root = LoadSettingsRoot(settingsPath);

        RemoveValue(root, "security", "auth", "selectedType");
        RemoveValue(root, "security", "auth", "apiKey");
        RemoveValue(root, "security", "auth", "baseUrl");
        RemoveValue(root, "codingPlan");
        RemoveValue(root, "env", CodingPlanEnvKey);
        SaveSettingsRoot(settingsPath, root);

        if (request.ClearPersistedCredentials)
        {
            await tokenManager.ClearCredentialsAsync(cancellationToken);
        }

        return PublishStatus(paths);
    }

    private JsonObject LoadMergedSettings(QwenRuntimeProfile runtimeProfile)
    {
        var merged = new JsonObject();
        foreach (var settingsPath in GetSettingsPaths(runtimeProfile))
        {
            if (!File.Exists(settingsPath))
            {
                continue;
            }

            try
            {
                if (JsonNode.Parse(File.ReadAllText(settingsPath)) is JsonObject layer)
                {
                    MergeObjects(merged, layer);
                }
            }
            catch
            {
            }
        }

        return merged;
    }

    private IEnumerable<string> GetSettingsPaths(QwenRuntimeProfile runtimeProfile)
    {
        var programDataRoot = ResolveProgramDataRoot();
        yield return GetSystemDefaultsPath(programDataRoot);
        yield return Path.Combine(runtimeProfile.GlobalQwenDirectory, "settings.json");
        yield return Path.Combine(runtimeProfile.ProjectRoot, ".qwen", "settings.json");
        yield return GetSystemSettingsPath(programDataRoot);
    }

    private string ResolveSelectedScope(QwenRuntimeProfile runtimeProfile)
    {
        var projectSettingsPath = Path.Combine(runtimeProfile.ProjectRoot, ".qwen", "settings.json");
        var userSettingsPath = Path.Combine(runtimeProfile.GlobalQwenDirectory, "settings.json");

        return SettingsHasSelectedType(projectSettingsPath)
            ? "project"
            : SettingsHasSelectedType(userSettingsPath)
                ? "user"
                : "user";
    }

    private static bool SettingsHasSelectedType(string settingsPath)
    {
        if (!File.Exists(settingsPath))
        {
            return false;
        }

        try
        {
            var root = JsonNode.Parse(File.ReadAllText(settingsPath)) as JsonObject;
            return !string.IsNullOrWhiteSpace(GetString(root, "security", "auth", "selectedType"));
        }
        catch
        {
            return false;
        }
    }

    private static string ResolveDisplayName(JsonObject mergedSettings, string selectedType)
    {
        if (string.Equals(selectedType, "qwen-oauth", StringComparison.OrdinalIgnoreCase))
        {
            return "Qwen OAuth";
        }

        return !string.IsNullOrWhiteSpace(GetString(mergedSettings, "codingPlan", "region"))
            ? "Alibaba Cloud Coding Plan"
            : "OpenAI-compatible";
    }

    private static string ResolveEndpoint(JsonObject mergedSettings, string selectedType, string model)
    {
        if (string.Equals(selectedType, "qwen-oauth", StringComparison.OrdinalIgnoreCase))
        {
            return "https://dashscope.aliyuncs.com/compatible-mode/v1/chat/completions";
        }

        var baseUrl = FirstNonEmpty(
            FindModelProviderBaseUrl(mergedSettings, selectedType, model),
            GetString(mergedSettings, "security", "auth", "baseUrl"),
            "https://dashscope.aliyuncs.com/compatible-mode/v1");
        return EnsureChatCompletionsEndpoint(baseUrl);
    }

    private static string ResolveApiKeyEnvironmentVariable(JsonObject mergedSettings, string selectedType, string model)
    {
        if (string.Equals(selectedType, "qwen-oauth", StringComparison.OrdinalIgnoreCase))
        {
            return "QWEN_OAUTH_ACCESS_TOKEN";
        }

        return FirstNonEmpty(
            FindModelProviderEnvKey(mergedSettings, selectedType, model),
            "OPENAI_API_KEY");
    }

    private static bool ResolveHasApiKey(
        JsonObject mergedSettings,
        string selectedType,
        string environmentVariableName,
        QwenOAuthCredentials? qwenCredentials)
    {
        if (string.Equals(selectedType, "qwen-oauth", StringComparison.OrdinalIgnoreCase))
        {
            return !string.IsNullOrWhiteSpace(qwenCredentials?.AccessToken);
        }

        var settingsEnv = GetNode(mergedSettings, ["env"]) as JsonObject;
        var configuredInSettingsEnv = settingsEnv?[environmentVariableName]?.GetValue<string?>() ?? string.Empty;
        var configuredInProcessEnv = Environment.GetEnvironmentVariable(environmentVariableName) ?? string.Empty;
        var configuredInline = GetString(mergedSettings, "security", "auth", "apiKey");

        return !string.IsNullOrWhiteSpace(FirstNonEmpty(configuredInProcessEnv, configuredInSettingsEnv, configuredInline));
    }

    private static string BuildMissingCredentialMessage(string selectedType, string environmentVariableName) =>
        string.Equals(selectedType, "qwen-oauth", StringComparison.OrdinalIgnoreCase)
            ? "Qwen OAuth credentials are not configured."
            : $"Missing API key. Set '{environmentVariableName}' or configure an inline API key.";

    private string ResolveMissingCredentialMessage(string selectedType, string environmentVariableName) =>
        string.Equals(selectedType, "qwen-oauth", StringComparison.OrdinalIgnoreCase) &&
        !string.IsNullOrWhiteSpace(tokenManager.LastError)
            ? tokenManager.LastError
            : BuildMissingCredentialMessage(selectedType, environmentVariableName);

    private AuthStatusSnapshot PublishStatus(WorkspacePaths paths)
    {
        var snapshot = GetStatus(paths);
        AuthChanged?.Invoke(this, snapshot);
        return snapshot;
    }

    private QwenOAuthDeviceFlowSnapshot? GetDeviceFlowSnapshot(string projectRoot)
    {
        lock (lifecycleSync)
        {
            return deviceFlows.Values
                .Where(flow => string.Equals(flow.ProjectRoot, projectRoot, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(flow => flow.StartedAtUtc)
                .Select(ToSnapshot)
                .FirstOrDefault();
        }
    }

    private DeviceFlowState? ResolveDeviceFlow(WorkspacePaths paths, string flowId)
    {
        var runtimeProfile = runtimeProfileService.Inspect(paths);

        if (!string.IsNullOrWhiteSpace(flowId) &&
            deviceFlows.TryGetValue(flowId, out var flow))
        {
            return flow;
        }

        return deviceFlows.Values
            .Where(item => string.Equals(item.ProjectRoot, runtimeProfile.ProjectRoot, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(item => item.StartedAtUtc)
            .FirstOrDefault();
    }

    private static string FindModelProviderBaseUrl(JsonObject mergedSettings, string authType, string modelId) =>
        FindModelProviderObject(mergedSettings, authType, modelId)?["baseUrl"]?.GetValue<string?>() ?? string.Empty;

    private static string FindModelProviderEnvKey(JsonObject mergedSettings, string authType, string modelId) =>
        FindModelProviderObject(mergedSettings, authType, modelId)?["envKey"]?.GetValue<string?>() ?? string.Empty;

    private static JsonObject? FindModelProviderObject(JsonObject mergedSettings, string authType, string modelId)
    {
        if (string.IsNullOrWhiteSpace(modelId) ||
            GetNode(mergedSettings, ["modelProviders", authType]) is not JsonArray providers)
        {
            return null;
        }

        return providers
            .OfType<JsonObject>()
            .FirstOrDefault(provider => string.Equals(provider["id"]?.GetValue<string?>(), modelId, StringComparison.OrdinalIgnoreCase));
    }

    private static string ResolveSettingsPath(QwenRuntimeProfile runtimeProfile, string scope) =>
        string.Equals(scope, "project", StringComparison.OrdinalIgnoreCase)
            ? Path.Combine(runtimeProfile.ProjectRoot, ".qwen", "settings.json")
            : Path.Combine(runtimeProfile.GlobalQwenDirectory, "settings.json");

    private static JsonObject LoadSettingsRoot(string path)
    {
        if (!File.Exists(path))
        {
            return [];
        }

        return JsonNode.Parse(File.ReadAllText(path)) as JsonObject ?? [];
    }

    private static void SaveSettingsRoot(string path, JsonObject root)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    private static void UpsertModelProvider(JsonObject root, string authType, string model, string baseUrl, string envKey)
    {
        if (string.IsNullOrWhiteSpace(model))
        {
            return;
        }

        var modelProviders = root["modelProviders"] as JsonObject ?? [];
        root["modelProviders"] = modelProviders;
        var authProviders = modelProviders[authType] as JsonArray ?? [];
        modelProviders[authType] = authProviders;

        var existing = authProviders
            .OfType<JsonObject>()
            .FirstOrDefault(provider => string.Equals(provider["id"]?.GetValue<string?>(), model, StringComparison.OrdinalIgnoreCase));

        existing ??= new JsonObject();
        existing["id"] = model;
        existing["baseUrl"] = string.IsNullOrWhiteSpace(baseUrl) ? null : baseUrl;
        existing["envKey"] = string.IsNullOrWhiteSpace(envKey) ? null : envKey;

        if (!authProviders.OfType<JsonObject>().Contains(existing))
        {
            authProviders.Add(existing);
        }
    }

    private static void ReplaceCodingPlanProviders(JsonObject root, IReadOnlyList<CodingPlanModel> template)
    {
        var modelProviders = root["modelProviders"] as JsonObject ?? [];
        root["modelProviders"] = modelProviders;
        var openAiProviders = modelProviders["openai"] as JsonArray ?? [];

        var updated = new JsonArray();
        foreach (var model in template)
        {
            updated.Add(
                new JsonObject
                {
                    ["id"] = model.Id,
                    ["name"] = model.Name,
                    ["baseUrl"] = model.BaseUrl,
                    ["envKey"] = CodingPlanEnvKey,
                    ["generationConfig"] = model.GenerationConfig.DeepClone()
                });
        }

        foreach (var provider in openAiProviders.OfType<JsonObject>().Where(static provider => !IsCodingPlanProvider(provider)))
        {
            updated.Add(provider.DeepClone());
        }

        modelProviders["openai"] = updated;
    }

    private static bool IsCodingPlanProvider(JsonObject provider) =>
        string.Equals(provider["envKey"]?.GetValue<string?>(), CodingPlanEnvKey, StringComparison.OrdinalIgnoreCase) &&
        provider["baseUrl"]?.GetValue<string?>() is { } baseUrl &&
        (string.Equals(baseUrl, "https://coding.dashscope.aliyuncs.com/v1", StringComparison.OrdinalIgnoreCase) ||
         string.Equals(baseUrl, "https://coding-intl.dashscope.aliyuncs.com/v1", StringComparison.OrdinalIgnoreCase));

    private static IReadOnlyList<CodingPlanModel> CreateCodingPlanTemplate(string region)
    {
        var baseUrl = string.Equals(region, "global", StringComparison.OrdinalIgnoreCase)
            ? "https://coding-intl.dashscope.aliyuncs.com/v1"
            : "https://coding.dashscope.aliyuncs.com/v1";
        var prefix = string.Equals(region, "global", StringComparison.OrdinalIgnoreCase)
            ? "[ModelStudio Coding Plan for Global/Intl]"
            : "[ModelStudio Coding Plan]";

        return
        [
            new CodingPlanModel("qwen3-coder-plus", $"{prefix} qwen3-coder-plus", baseUrl, new JsonObject { ["contextWindowSize"] = 1000000 }),
            new CodingPlanModel("qwen3-coder-next", $"{prefix} qwen3-coder-next", baseUrl, new JsonObject { ["contextWindowSize"] = 262144 }),
            new CodingPlanModel("qwen3.5-plus", $"{prefix} qwen3.5-plus", baseUrl, new JsonObject
            {
                ["extra_body"] = new JsonObject { ["enable_thinking"] = true },
                ["contextWindowSize"] = 1000000
            })
        ];
    }

    private static string ComputeTemplateVersion(IReadOnlyList<CodingPlanModel> template)
    {
        var json = JsonSerializer.Serialize(template.Select(item => new { item.Id, item.Name, item.BaseUrl }));
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private string ResolveProgramDataRoot() =>
        Environment.GetEnvironmentVariable("QWEN_CODE_SYSTEM_SETTINGS_PATH") is { Length: > 0 } overridePath
            ? Path.GetDirectoryName(overridePath) ?? string.Empty
            : environmentPaths.ProgramDataDirectory is { Length: > 0 } commonAppData
                ? Path.Combine(commonAppData, "qwen-code")
                : string.Empty;

    private static string GetSystemDefaultsPath(string programDataRoot)
    {
        var overridePath = Environment.GetEnvironmentVariable("QWEN_CODE_SYSTEM_DEFAULTS_PATH");
        return string.IsNullOrWhiteSpace(overridePath)
            ? Path.Combine(programDataRoot, "system-defaults.json")
            : overridePath;
    }

    private static string GetSystemSettingsPath(string programDataRoot)
    {
        var overridePath = Environment.GetEnvironmentVariable("QWEN_CODE_SYSTEM_SETTINGS_PATH");
        return string.IsNullOrWhiteSpace(overridePath)
            ? Path.Combine(programDataRoot, "settings.json")
            : overridePath;
    }

    private static void MergeObjects(JsonObject target, JsonObject source)
    {
        foreach (var pair in source)
        {
            if (pair.Value is JsonObject sourceObject)
            {
                if (target[pair.Key] is not JsonObject targetObject)
                {
                    targetObject = new JsonObject();
                    target[pair.Key] = targetObject;
                }

                MergeObjects(targetObject, sourceObject);
            }
            else
            {
                target[pair.Key] = pair.Value?.DeepClone();
            }
        }
    }

    private static void SetValue(JsonObject root, string path0, string path1, string path2, string value)
    {
        var target = EnsurePath(root, [path0, path1]);
        target[path2] = string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static void SetValue(JsonObject root, string path0, string path1, string value)
    {
        var target = EnsurePath(root, [path0]);
        target[path1] = string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static void RemoveValue(JsonObject root, params string[] path)
    {
        if (path.Length == 0)
        {
            return;
        }

        if (path.Length == 1)
        {
            root.Remove(path[0]);
            return;
        }

        if (GetNode(root, path[..^1]) is JsonObject parent)
        {
            parent.Remove(path[^1]);
        }
    }

    private static JsonObject EnsurePath(JsonObject root, IReadOnlyList<string> path)
    {
        var current = root;
        foreach (var segment in path)
        {
            if (current[segment] is not JsonObject next)
            {
                next = new JsonObject();
                current[segment] = next;
            }

            current = next;
        }

        return current;
    }

    private static JsonNode? GetNode(JsonObject? root, IReadOnlyList<string> path)
    {
        JsonNode? current = root;
        foreach (var segment in path)
        {
            if (current is not JsonObject currentObject ||
                !currentObject.TryGetPropertyValue(segment, out current))
            {
                return null;
            }
        }

        return current;
    }

    private static string GetString(JsonObject? root, params string[] path) =>
        GetNode(root, path) is JsonValue value && value.TryGetValue<string>(out var result)
            ? result ?? string.Empty
            : string.Empty;

    private static string FirstNonEmpty(params string?[] candidates) =>
        candidates.FirstOrDefault(static candidate => !string.IsNullOrWhiteSpace(candidate)) ?? string.Empty;

    private static string EnsureChatCompletionsEndpoint(string baseUrl) =>
        string.IsNullOrWhiteSpace(baseUrl)
            ? string.Empty
            : baseUrl.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase)
                ? baseUrl
                : $"{baseUrl.TrimEnd('/')}/chat/completions";

    private async Task PollDeviceFlowAsync(WorkspacePaths paths, DeviceFlowState state)
    {
        try
        {
            while (!state.Cancellation.IsCancellationRequested && DateTimeOffset.UtcNow < state.ExpiresAtUtc)
            {
                var tokenResponse = await PollDeviceTokenAsync(state, state.Cancellation.Token);
                if (tokenResponse.Status == DeviceTokenStatus.Pending)
                {
                    if (tokenResponse.SlowDown)
                    {
                        state.PollIntervalMs = Math.Min(10000, (int)Math.Ceiling(state.PollIntervalMs * 1.5));
                    }

                    PublishStatus(paths);
                    await Task.Delay(state.PollIntervalMs, state.Cancellation.Token);
                    continue;
                }

                if (tokenResponse.Status == DeviceTokenStatus.Success)
                {
                    state.Status = "succeeded";
                    state.CompletedAtUtc = DateTimeOffset.UtcNow;
                    state.ErrorMessage = string.Empty;

                    await ConfigureQwenOAuthAsync(
                        paths,
                        new ConfigureQwenOAuthRequest
                        {
                            Scope = state.Scope,
                            AccessToken = tokenResponse.AccessToken,
                            RefreshToken = tokenResponse.RefreshToken,
                            TokenType = tokenResponse.TokenType,
                            ResourceUrl = tokenResponse.ResourceUrl,
                            ExpiresAtUtc = tokenResponse.ExpiresAtUtc
                        },
                        state.Cancellation.Token);

                    PublishStatus(paths);
                    return;
                }

                state.Status = "error";
                state.ErrorMessage = tokenResponse.ErrorMessage;
                state.CompletedAtUtc = DateTimeOffset.UtcNow;
                PublishStatus(paths);
                return;
            }

            if (!state.Cancellation.IsCancellationRequested && state.CompletedAtUtc is null)
            {
                state.Status = "timeout";
                state.ErrorMessage = "Authentication timed out. Please try again.";
                state.CompletedAtUtc = DateTimeOffset.UtcNow;
                PublishStatus(paths);
            }
        }
        catch (OperationCanceledException)
        {
            if (state.CompletedAtUtc is null)
            {
                state.Status = "cancelled";
                state.ErrorMessage = "Authentication cancelled by user.";
                state.CompletedAtUtc = DateTimeOffset.UtcNow;
                PublishStatus(paths);
            }
        }
        catch (Exception exception)
        {
            state.Status = "error";
            state.ErrorMessage = exception.Message;
            state.CompletedAtUtc = DateTimeOffset.UtcNow;
            PublishStatus(paths);
        }
    }

    private async Task<DeviceAuthorizationResponse> RequestDeviceAuthorizationAsync(
        string codeChallenge,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, QwenOAuthDeviceCodeEndpoint)
        {
            Content = new FormUrlEncodedContent(
            [
                new KeyValuePair<string, string>("client_id", QwenOAuthClientId),
                new KeyValuePair<string, string>("scope", QwenOAuthScope),
                new KeyValuePair<string, string>("code_challenge", codeChallenge),
                new KeyValuePair<string, string>("code_challenge_method", "S256")
            ])
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.TryAddWithoutValidation("x-request-id", Guid.NewGuid().ToString());

        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var root = document.RootElement;
        return new DeviceAuthorizationResponse
        {
            DeviceCode = root.GetProperty("device_code").GetString() ?? string.Empty,
            UserCode = root.GetProperty("user_code").GetString() ?? string.Empty,
            VerificationUri = root.GetProperty("verification_uri").GetString() ?? string.Empty,
            VerificationUriComplete = root.GetProperty("verification_uri_complete").GetString() ?? string.Empty,
            ExpiresIn = root.GetProperty("expires_in").GetInt32()
        };
    }

    private async Task<DeviceTokenResponse> PollDeviceTokenAsync(DeviceFlowState state, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, QwenOAuthTokenEndpoint)
        {
            Content = new FormUrlEncodedContent(
            [
                new KeyValuePair<string, string>("grant_type", QwenOAuthGrantType),
                new KeyValuePair<string, string>("client_id", QwenOAuthClientId),
                new KeyValuePair<string, string>("device_code", state.DeviceCode),
                new KeyValuePair<string, string>("code_verifier", state.CodeVerifier)
            ])
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await httpClient.SendAsync(request, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            using var errorDocument = JsonDocument.Parse(content);
            var errorRoot = errorDocument.RootElement;
            var error = errorRoot.TryGetProperty("error", out var errorProperty)
                ? errorProperty.GetString() ?? string.Empty
                : string.Empty;
            if (string.Equals(error, "authorization_pending", StringComparison.OrdinalIgnoreCase))
            {
                return DeviceTokenResponse.Pending();
            }

            return DeviceTokenResponse.Error(
                errorRoot.TryGetProperty("error_description", out var descriptionProperty)
                    ? descriptionProperty.GetString() ?? "Qwen OAuth device flow failed."
                    : "Qwen OAuth device flow failed.");
        }

        if ((int)response.StatusCode == 429)
        {
            return DeviceTokenResponse.Pending(slowDown: true);
        }

        if (!response.IsSuccessStatusCode)
        {
            return DeviceTokenResponse.Error($"Qwen OAuth polling failed with status {(int)response.StatusCode}.");
        }

        using var document = JsonDocument.Parse(content);
        var root = document.RootElement;
        var expiresIn = root.TryGetProperty("expires_in", out var expiresProperty) && expiresProperty.ValueKind == JsonValueKind.Number
            ? expiresProperty.GetInt32()
            : 0;

        return DeviceTokenResponse.Success(
            root.GetProperty("access_token").GetString() ?? string.Empty,
            root.TryGetProperty("refresh_token", out var refreshProperty) ? refreshProperty.GetString() ?? string.Empty : string.Empty,
            root.TryGetProperty("token_type", out var tokenTypeProperty) ? tokenTypeProperty.GetString() ?? "Bearer" : "Bearer",
            root.TryGetProperty("resource_url", out var resourceProperty) ? resourceProperty.GetString() ?? string.Empty : string.Empty,
            expiresIn > 0 ? DateTimeOffset.UtcNow.AddSeconds(expiresIn) : null);
    }

    private static QwenOAuthDeviceFlowSnapshot ToSnapshot(DeviceFlowState flow) =>
        new()
        {
            FlowId = flow.FlowId,
            Scope = flow.Scope,
            Status = flow.Status,
            VerificationUri = flow.VerificationUri,
            VerificationUriComplete = flow.VerificationUriComplete,
            UserCode = flow.UserCode,
            PollIntervalMs = flow.PollIntervalMs,
            BrowserLaunchAttempted = flow.BrowserLaunchAttempted,
            BrowserLaunchSucceeded = flow.BrowserLaunchSucceeded,
            StartedAtUtc = flow.StartedAtUtc,
            ExpiresAtUtc = flow.ExpiresAtUtc,
            CompletedAtUtc = flow.CompletedAtUtc,
            ErrorMessage = flow.ErrorMessage
        };

    private static PkcePair GeneratePkcePair()
    {
        var verifier = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(verifier));
        var challenge = Convert.ToBase64String(hash)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
        return new PkcePair(verifier, challenge);
    }

    private sealed record PkcePair(string CodeVerifier, string CodeChallenge);
    private sealed record DeviceAuthorizationResponse
    {
        public required string DeviceCode { get; init; }
        public required string UserCode { get; init; }
        public required string VerificationUri { get; init; }
        public required string VerificationUriComplete { get; init; }
        public required int ExpiresIn { get; init; }
    }

    private sealed class DeviceFlowState
    {
        public required string FlowId { get; init; }
        public required string Scope { get; init; }
        public required string ProjectRoot { get; init; }
        public required string DeviceCode { get; init; }
        public required string CodeVerifier { get; init; }
        public required string VerificationUri { get; init; }
        public required string VerificationUriComplete { get; init; }
        public required string UserCode { get; init; }
        public required DateTimeOffset StartedAtUtc { get; init; }
        public required DateTimeOffset ExpiresAtUtc { get; init; }
        public required bool BrowserLaunchAttempted { get; init; }
        public required bool BrowserLaunchSucceeded { get; init; }
        public required CancellationTokenSource Cancellation { get; init; }
        public required string Status { get; set; }
        public required int PollIntervalMs { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public DateTimeOffset? CompletedAtUtc { get; set; }
    }

    private enum DeviceTokenStatus
    {
        Pending,
        Success,
        Error
    }

    private sealed record DeviceTokenResponse
    {
        public required DeviceTokenStatus Status { get; init; }
        public bool SlowDown { get; init; }
        public string AccessToken { get; init; } = string.Empty;
        public string RefreshToken { get; init; } = string.Empty;
        public string TokenType { get; init; } = "Bearer";
        public string ResourceUrl { get; init; } = string.Empty;
        public DateTimeOffset? ExpiresAtUtc { get; init; }
        public string ErrorMessage { get; init; } = string.Empty;

        public static DeviceTokenResponse Pending(bool slowDown = false) =>
            new() { Status = DeviceTokenStatus.Pending, SlowDown = slowDown };

        public static DeviceTokenResponse Success(
            string accessToken,
            string refreshToken,
            string tokenType,
            string resourceUrl,
            DateTimeOffset? expiresAtUtc) =>
            new()
            {
                Status = DeviceTokenStatus.Success,
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                TokenType = tokenType,
                ResourceUrl = resourceUrl,
                ExpiresAtUtc = expiresAtUtc
            };

        public static DeviceTokenResponse Error(string errorMessage) =>
            new()
            {
                Status = DeviceTokenStatus.Error,
                ErrorMessage = errorMessage
            };
    }

    private sealed record CodingPlanModel(string Id, string Name, string BaseUrl, JsonObject GenerationConfig);
}
