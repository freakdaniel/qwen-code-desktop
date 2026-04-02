using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using QwenCode.App.Compatibility;
using QwenCode.App.Infrastructure;
using QwenCode.App.Models;

namespace QwenCode.App.Tools;

public sealed class WebToolService(
    IDesktopEnvironmentPaths environmentPaths,
    HttpClient? httpClient = null) : IWebToolService
{
    private const int FetchTimeoutMilliseconds = 10_000;
    private const int MaxFetchContentLength = 100_000;
    private const int DefaultSearchResultCount = 5;

    private readonly HttpClient client = httpClient ?? CreateDefaultClient();

    public async Task<string> FetchAsync(
        QwenRuntimeProfile runtimeProfile,
        JsonElement arguments,
        CancellationToken cancellationToken = default)
    {
        var url = RequireString(arguments, "url");
        var prompt = RequireString(arguments, "prompt");
        var normalizedUrl = NormalizeFetchUrl(url);

        using var response = await SendAsync(normalizedUrl, cancellationToken);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        EnsureSuccess(response, payload);

        var contentType = response.Content.Headers.ContentType?.MediaType;
        var extractedText = ExtractReadableText(payload, contentType);
        if (string.IsNullOrWhiteSpace(extractedText))
        {
            extractedText = "(No readable content extracted from the response.)";
        }

        var promptScopedExcerpt = BuildPromptScopedExcerpt(prompt, extractedText);
        return $"""
Fetched content from {normalizedUrl}
Requested analysis: {prompt}

{promptScopedExcerpt}
""";
    }

    public async Task<string> SearchAsync(
        QwenRuntimeProfile runtimeProfile,
        JsonElement arguments,
        CancellationToken cancellationToken = default)
    {
        var query = RequireString(arguments, "query");
        var requestedProvider = TryGetString(arguments, "provider");
        var configuration = ResolveWebSearchConfiguration(runtimeProfile.ProjectRoot);

        if (configuration is null)
        {
            return "Web search is disabled. Configure a webSearch provider in your qwen-compatible settings.json.";
        }

        var provider = SelectProvider(configuration, requestedProvider);
        var result = await (provider switch
        {
            WebSearchProviderConfiguration { Type: "tavily" } tavily =>
                SearchWithTavilyAsync(query, tavily, cancellationToken),
            WebSearchProviderConfiguration { Type: "google" } google =>
                SearchWithGoogleAsync(query, google, cancellationToken),
            WebSearchProviderConfiguration { Type: "dashscope" } =>
                throw new InvalidOperationException("DashScope web search is not wired in the C# port yet."),
            _ => throw new InvalidOperationException($"Unknown web search provider '{provider.Type}'.")
        });

        if (!result.Results.Any() && string.IsNullOrWhiteSpace(result.Answer))
        {
            return $"No search results found for query: \"{query}\" (via {provider.Type})";
        }

        var content = BuildSearchContent(result);
        return $"Web search results for \"{query}\" (via {provider.Type}):{Environment.NewLine}{Environment.NewLine}{content}";
    }

    private async Task<HttpResponseMessage> SendAsync(string url, CancellationToken cancellationToken)
    {
        using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        linkedSource.CancelAfter(FetchTimeoutMilliseconds);

        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.UserAgent.ParseAdd("QwenCodeDesktop/0.1");
        return await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, linkedSource.Token);
    }

    private static HttpClient CreateDefaultClient()
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("QwenCodeDesktop", "0.1"));
        return client;
    }

    private static void EnsureSuccess(HttpResponseMessage response, string body)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var snippet = string.IsNullOrWhiteSpace(body)
            ? string.Empty
            : $" - {body[..Math.Min(body.Length, 200)]}";
        throw new InvalidOperationException($"Request failed with status code {(int)response.StatusCode} {response.ReasonPhrase}{snippet}");
    }

    private static string NormalizeFetchUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException("Parameter 'url' must be an absolute http:// or https:// URL.");
        }

        if (uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase) &&
            uri.AbsolutePath.Contains("/blob/", StringComparison.OrdinalIgnoreCase))
        {
            return url
                .Replace("github.com", "raw.githubusercontent.com", StringComparison.OrdinalIgnoreCase)
                .Replace("/blob/", "/", StringComparison.OrdinalIgnoreCase);
        }

        return uri.ToString();
    }

    private static string ExtractReadableText(string content, string? mediaType)
    {
        if (!string.IsNullOrWhiteSpace(mediaType) &&
            !mediaType.Contains("html", StringComparison.OrdinalIgnoreCase) &&
            !mediaType.Contains("xml", StringComparison.OrdinalIgnoreCase))
        {
            return content.Length > MaxFetchContentLength ? content[..MaxFetchContentLength] : content;
        }

        var normalized = content;
        normalized = Regex.Replace(normalized, "<script\\b[^>]*>.*?</script>", string.Empty, RegexOptions.IgnoreCase | RegexOptions.Singleline);
        normalized = Regex.Replace(normalized, "<style\\b[^>]*>.*?</style>", string.Empty, RegexOptions.IgnoreCase | RegexOptions.Singleline);
        normalized = Regex.Replace(normalized, "<(br|/p|/div|/li|/section|/article|/h\\d)\\b[^>]*>", Environment.NewLine, RegexOptions.IgnoreCase);
        normalized = Regex.Replace(normalized, "<[^>]+>", " ");
        normalized = WebUtility.HtmlDecode(normalized);
        normalized = Regex.Replace(normalized, @"[ \t]+\r?\n", Environment.NewLine);
        normalized = Regex.Replace(normalized, @"\r?\n\s*\r?\n\s*\r?\n+", Environment.NewLine + Environment.NewLine);
        normalized = Regex.Replace(normalized, @"[ \t]{2,}", " ");
        normalized = normalized.Trim();

        return normalized.Length > MaxFetchContentLength ? normalized[..MaxFetchContentLength] : normalized;
    }

    private static string BuildPromptScopedExcerpt(string prompt, string extractedText)
    {
        var promptTerms = Regex.Matches(prompt.ToLowerInvariant(), "[a-zA-Zа-яА-Я0-9_\\-]{4,}")
            .Select(match => match.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var paragraphs = extractedText
            .Split([Environment.NewLine + Environment.NewLine, "\n\n"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(static paragraph => !string.IsNullOrWhiteSpace(paragraph))
            .ToArray();

        var selected = paragraphs
            .Select(paragraph => new
            {
                Paragraph = paragraph,
                Score = promptTerms.Count(term => paragraph.Contains(term, StringComparison.OrdinalIgnoreCase))
            })
            .Where(item => item.Score > 0)
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Paragraph.Length)
            .Take(4)
            .Select(item => item.Paragraph)
            .ToArray();

        if (selected.Length == 0)
        {
            selected = paragraphs.Take(4).ToArray();
        }

        if (selected.Length == 0)
        {
            return extractedText[..Math.Min(extractedText.Length, 1500)];
        }

        var result = string.Join(Environment.NewLine + Environment.NewLine, selected);
        return result.Length > 4_000 ? result[..4_000] : result;
    }

    private WebSearchConfiguration? ResolveWebSearchConfiguration(string projectRoot)
    {
        var merged = LoadMergedSettings(projectRoot);
        if (GetNode(merged, "webSearch") is not JsonObject webSearchObject)
        {
            return null;
        }

        var settingsEnvironment = ReadEnvironment(merged);
        var providers = new List<WebSearchProviderConfiguration>();
        var defaultProvider = GetString(webSearchObject, "default");

        if (GetNode(webSearchObject, "provider") is not JsonArray providerArray)
        {
            return null;
        }

        foreach (var providerNode in providerArray.OfType<JsonObject>())
        {
            var type = GetString(providerNode, "type");
            if (string.IsNullOrWhiteSpace(type))
            {
                continue;
            }

            var apiKey = FirstNonEmpty(
                GetString(providerNode, "apiKey"),
                type.Equals("tavily", StringComparison.OrdinalIgnoreCase)
                    ? ReadEnvironmentValue(settingsEnvironment, "TAVILY_API_KEY", "WEB_SEARCH_API_KEY")
                    : type.Equals("google", StringComparison.OrdinalIgnoreCase)
                        ? ReadEnvironmentValue(settingsEnvironment, "GOOGLE_SEARCH_API_KEY", "GOOGLE_API_KEY")
                        : ReadEnvironmentValue(settingsEnvironment, "DASHSCOPE_API_KEY"),
                type.Equals("tavily", StringComparison.OrdinalIgnoreCase)
                    ? Environment.GetEnvironmentVariable("TAVILY_API_KEY")
                    : type.Equals("google", StringComparison.OrdinalIgnoreCase)
                        ? FirstNonEmpty(Environment.GetEnvironmentVariable("GOOGLE_SEARCH_API_KEY"), Environment.GetEnvironmentVariable("GOOGLE_API_KEY"))
                        : Environment.GetEnvironmentVariable("DASHSCOPE_API_KEY"));

            var searchEngineId = FirstNonEmpty(
                GetString(providerNode, "searchEngineId"),
                ReadEnvironmentValue(settingsEnvironment, "GOOGLE_SEARCH_ENGINE_ID", "GOOGLE_CSE_ID"),
                Environment.GetEnvironmentVariable("GOOGLE_SEARCH_ENGINE_ID"),
                Environment.GetEnvironmentVariable("GOOGLE_CSE_ID"));

            providers.Add(new WebSearchProviderConfiguration(
                Type: type,
                ApiKey: apiKey,
                SearchEngineId: searchEngineId,
                MaxResults: TryGetInt(providerNode, "maxResults") ?? DefaultSearchResultCount,
                SearchDepth: FirstNonEmpty(GetString(providerNode, "searchDepth"), "advanced"),
                IncludeAnswer: TryGetBool(providerNode, "includeAnswer") ?? true,
                SafeSearch: FirstNonEmpty(GetString(providerNode, "safeSearch"), "medium"),
                Language: GetString(providerNode, "language"),
                Country: GetString(providerNode, "country")));
        }

        return providers.Count == 0
            ? null
            : new WebSearchConfiguration(providers, FirstNonEmpty(defaultProvider, providers[0].Type));
    }

    private async Task<WebSearchResult> SearchWithTavilyAsync(
        string query,
        WebSearchProviderConfiguration provider,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(provider.ApiKey))
        {
            throw new InvalidOperationException("Tavily provider is missing apiKey.");
        }

        using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        linkedSource.CancelAfter(FetchTimeoutMilliseconds);

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.tavily.com/search");
        request.Content = new StringContent(
            JsonSerializer.Serialize(new
            {
                api_key = provider.ApiKey,
                query,
                search_depth = provider.SearchDepth,
                max_results = provider.MaxResults,
                include_answer = provider.IncludeAnswer
            }),
            Encoding.UTF8,
            "application/json");

        using var response = await client.SendAsync(request, linkedSource.Token);
        var body = await response.Content.ReadAsStringAsync(linkedSource.Token);
        EnsureSuccess(response, body);

        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        var answer = TryGetString(root, "answer");
        var results = root.TryGetProperty("results", out var resultsElement) && resultsElement.ValueKind == JsonValueKind.Array
            ? resultsElement.EnumerateArray().Select(item => new WebSearchResultItem(
                Title: TryGetString(item, "title") ?? "Untitled",
                Url: TryGetString(item, "url") ?? string.Empty,
                Content: TryGetString(item, "content"),
                Score: TryGetDouble(item, "score"),
                PublishedDate: TryGetString(item, "published_date"))).ToArray()
            : [];

        return new WebSearchResult(answer, results);
    }

    private async Task<WebSearchResult> SearchWithGoogleAsync(
        string query,
        WebSearchProviderConfiguration provider,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(provider.ApiKey) || string.IsNullOrWhiteSpace(provider.SearchEngineId))
        {
            throw new InvalidOperationException("Google provider requires apiKey and searchEngineId.");
        }

        using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        linkedSource.CancelAfter(FetchTimeoutMilliseconds);

        var parameters = new Dictionary<string, string?>
        {
            ["key"] = provider.ApiKey,
            ["cx"] = provider.SearchEngineId,
            ["q"] = query,
            ["num"] = provider.MaxResults.ToString(),
            ["safe"] = provider.SafeSearch
        };

        if (!string.IsNullOrWhiteSpace(provider.Language))
        {
            parameters["lr"] = $"lang_{provider.Language}";
        }

        if (!string.IsNullOrWhiteSpace(provider.Country))
        {
            parameters["cr"] = $"country{provider.Country}";
        }

        var queryString = string.Join("&", parameters
            .Where(static pair => !string.IsNullOrWhiteSpace(pair.Value))
            .Select(static pair => $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value!)}"));

        using var response = await client.GetAsync($"https://www.googleapis.com/customsearch/v1?{queryString}", linkedSource.Token);
        var body = await response.Content.ReadAsStringAsync(linkedSource.Token);
        EnsureSuccess(response, body);

        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        var results = root.TryGetProperty("items", out var itemsElement) && itemsElement.ValueKind == JsonValueKind.Array
            ? itemsElement.EnumerateArray().Select(item => new WebSearchResultItem(
                Title: TryGetString(item, "title") ?? "Untitled",
                Url: TryGetString(item, "link") ?? string.Empty,
                Content: TryGetString(item, "snippet"),
                Score: null,
                PublishedDate: null)).ToArray()
            : [];

        return new WebSearchResult(null, results);
    }

    private static string BuildSearchContent(WebSearchResult result)
    {
        var sources = result.Results
            .Where(static item => !string.IsNullOrWhiteSpace(item.Url))
            .Select(static item => (item.Title, item.Url))
            .ToArray();

        if (!string.IsNullOrWhiteSpace(result.Answer))
        {
            var sourceLines = sources.Length == 0
                ? string.Empty
                : $"{Environment.NewLine}{Environment.NewLine}Sources:{Environment.NewLine}{string.Join(Environment.NewLine, sources.Select((source, index) => $"[{index + 1}] {source.Title} ({source.Url})"))}";
            return $"{result.Answer}{sourceLines}";
        }

        var summary = result.Results
            .Take(5)
            .Select((item, index) =>
            {
                var lines = new List<string> { $"{index + 1}. **{item.Title}**" };
                if (!string.IsNullOrWhiteSpace(item.Content))
                {
                    lines.Add($"   {item.Content.Trim()}");
                }

                lines.Add($"   Source: {item.Url}");
                if (item.Score is { } score)
                {
                    lines.Add($"   Relevance: {(score * 100):F0}%");
                }

                if (!string.IsNullOrWhiteSpace(item.PublishedDate))
                {
                    lines.Add($"   Published: {item.PublishedDate}");
                }

                return string.Join(Environment.NewLine, lines);
            });

        var content = string.Join(Environment.NewLine + Environment.NewLine, summary);
        return string.IsNullOrWhiteSpace(content)
            ? string.Empty
            : $"{content}{Environment.NewLine}{Environment.NewLine}*Note: For detailed content from any source above, use the web_fetch tool with the URL.*";
    }

    private JsonObject LoadMergedSettings(string projectRoot)
    {
        var merged = new JsonObject();
        foreach (var settingsPath in GetSettingsPaths(projectRoot))
        {
            if (!File.Exists(settingsPath))
            {
                continue;
            }

            try
            {
                using var stream = File.OpenRead(settingsPath);
                using var document = JsonDocument.Parse(stream);
                if (JsonNode.Parse(document.RootElement.GetRawText()) is JsonObject objectNode)
                {
                    MergeObjects(merged, objectNode);
                }
            }
            catch
            {
                // Ignore malformed settings layers.
            }
        }

        return merged;
    }

    private IEnumerable<string> GetSettingsPaths(string projectRoot)
    {
        var globalQwenDirectory = Path.Combine(environmentPaths.HomeDirectory, ".qwen");
        var programDataRoot = ResolveProgramDataRoot();

        yield return GetSystemDefaultsPath(programDataRoot);
        yield return Path.Combine(globalQwenDirectory, "settings.json");
        yield return Path.Combine(projectRoot, ".qwen", "settings.json");
        yield return GetSystemSettingsPath(programDataRoot);
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

    private static IReadOnlyDictionary<string, string> ReadEnvironment(JsonObject mergedSettings)
    {
        if (GetNode(mergedSettings, "env") is not JsonObject envObject)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        return envObject
            .Where(static pair => pair.Value is JsonValue)
            .Select(pair => new KeyValuePair<string, string?>(pair.Key, pair.Value?.GetValue<string>()))
            .Where(static pair => !string.IsNullOrWhiteSpace(pair.Value))
            .ToDictionary(static pair => pair.Key, static pair => pair.Value!, StringComparer.OrdinalIgnoreCase);
    }

    private static string ReadEnvironmentValue(IReadOnlyDictionary<string, string> settingsEnvironment, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (settingsEnvironment.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return string.Empty;
    }

    private static void MergeObjects(JsonObject target, JsonObject source)
    {
        foreach (var (key, value) in source)
        {
            if (value is JsonObject sourceObject &&
                target[key] is JsonObject targetObject)
            {
                MergeObjects(targetObject, sourceObject);
                continue;
            }

            target[key] = value?.DeepClone();
        }
    }

    private static JsonNode? GetNode(JsonObject root, params string[] path)
    {
        JsonNode? current = root;
        foreach (var segment in path)
        {
            if (current is not JsonObject objectNode ||
                !objectNode.TryGetPropertyValue(segment, out current) ||
                current is null)
            {
                return null;
            }
        }

        return current;
    }

    private static WebSearchProviderConfiguration SelectProvider(WebSearchConfiguration configuration, string? requestedProvider)
    {
        if (!string.IsNullOrWhiteSpace(requestedProvider))
        {
            return configuration.Providers.FirstOrDefault(provider =>
                       provider.Type.Equals(requestedProvider, StringComparison.OrdinalIgnoreCase))
                   ?? throw new InvalidOperationException($"The specified provider \"{requestedProvider}\" is not available.");
        }

        return configuration.Providers.FirstOrDefault(provider =>
                   provider.Type.Equals(configuration.DefaultProvider, StringComparison.OrdinalIgnoreCase))
               ?? configuration.Providers[0];
    }

    private static string RequireString(JsonElement arguments, string propertyName) =>
        TryGetString(arguments, propertyName) is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException($"Parameter '{propertyName}' is required.");

    private static string GetString(JsonObject root, string propertyName) =>
        root[propertyName]?.GetValue<string>() ?? string.Empty;

    private static int? TryGetInt(JsonObject root, string propertyName) =>
        root[propertyName]?.GetValue<int?>();

    private static bool? TryGetBool(JsonObject root, string propertyName) =>
        root[propertyName]?.GetValue<bool?>();

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

    private static string? TryGetString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    private static double? TryGetDouble(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.Number && property.TryGetDouble(out var value)
            ? value
            : null;

    private sealed record WebSearchConfiguration(
        IReadOnlyList<WebSearchProviderConfiguration> Providers,
        string DefaultProvider);

    private sealed record WebSearchProviderConfiguration(
        string Type,
        string ApiKey,
        string SearchEngineId,
        int MaxResults,
        string SearchDepth,
        bool IncludeAnswer,
        string SafeSearch,
        string Language,
        string Country);

    private sealed record WebSearchResult(
        string? Answer,
        IReadOnlyList<WebSearchResultItem> Results);

    private sealed record WebSearchResultItem(
        string Title,
        string Url,
        string? Content,
        double? Score,
        string? PublishedDate);
}
