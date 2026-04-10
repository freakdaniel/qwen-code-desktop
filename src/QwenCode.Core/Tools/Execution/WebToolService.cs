using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using QwenCode.Core.Compatibility;
using QwenCode.Core.Config;
using QwenCode.Core.Infrastructure;
using QwenCode.Core.Models;

namespace QwenCode.Core.Tools;

/// <summary>
/// Represents the Web Tool Service
/// </summary>
/// <param name="environmentPaths">The environment paths</param>
/// <param name="httpClient">The http client</param>
public sealed class WebToolService(
    IDesktopEnvironmentPaths environmentPaths,
    HttpClient? httpClient = null) : IWebToolService
{
    private const int FetchTimeoutMilliseconds = 60_000;
    private const int MaxFetchContentLength = 100_000;
    private const int DefaultSearchResultCount = 5;
    private const int MaxRedirects = 10;
    private static readonly TimeSpan FetchCacheLifetime = TimeSpan.FromMinutes(15);
    private static readonly ConcurrentDictionary<string, CachedFetchResult> FetchCache = new(StringComparer.OrdinalIgnoreCase);

    private readonly HttpClient client = httpClient ?? CreateDefaultClient();

    /// <summary>
    /// Executes fetch async
    /// </summary>
    /// <param name="runtimeProfile">The runtime profile</param>
    /// <param name="arguments">The arguments</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that resolves to string</returns>
    public async Task<string> FetchAsync(
        QwenRuntimeProfile runtimeProfile,
        JsonElement arguments,
        CancellationToken cancellationToken = default)
    {
        var url = RequireString(arguments, "url");
        var prompt = TryGetString(arguments, "prompt")?.Trim() ?? string.Empty;
        var normalizedUrl = NormalizeFetchUrl(url);
        var fetchResult = await FetchContentAsync(normalizedUrl, cancellationToken);

        if (fetchResult.CrossHostRedirect is { } redirect)
        {
            return BuildRedirectMessage(redirect, prompt);
        }

        var extractedText = fetchResult.ExtractedText;
        var effectiveUrl = fetchResult.EffectiveUrl;

        if (string.IsNullOrWhiteSpace(prompt))
        {
            return $"""
Fetched content from {effectiveUrl}

{BuildDefaultFetchExcerpt(extractedText)}
""";
        }

        var promptScopedExcerpt = BuildPromptScopedExcerpt(prompt, extractedText);
        return $"""
Fetched content from {effectiveUrl}
Requested analysis: {prompt}

{promptScopedExcerpt}
""";
    }

    /// <summary>
    /// Executes search async
    /// </summary>
    /// <param name="runtimeProfile">The runtime profile</param>
    /// <param name="arguments">The arguments</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that resolves to string</returns>
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

    private async Task<FetchedContentResult> FetchContentAsync(string url, CancellationToken cancellationToken)
    {
        if (TryGetCachedFetchResult(url, out var cached))
        {
            return cached;
        }

        var sendResult = await SendAsync(url, cancellationToken);
        if (sendResult.CrossHostRedirect is { } redirect)
        {
            CacheFetchResult(url, new FetchedContentResult(
                EffectiveUrl: url,
                ExtractedText: string.Empty,
                CrossHostRedirect: redirect));
            return new FetchedContentResult(
                EffectiveUrl: url,
                ExtractedText: string.Empty,
                CrossHostRedirect: redirect);
        }

        using var response = sendResult.Response!;
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        EnsureSuccess(response, payload);

        var contentType = response.Content.Headers.ContentType?.MediaType;
        var extractedText = ExtractReadableText(payload, contentType);
        if (string.IsNullOrWhiteSpace(extractedText))
        {
            extractedText = "(No readable content extracted from the response.)";
        }

        var result = new FetchedContentResult(
            EffectiveUrl: sendResult.EffectiveUrl ?? response.RequestMessage?.RequestUri?.ToString() ?? url,
            ExtractedText: extractedText,
            CrossHostRedirect: null);

        CacheFetchResult(url, result);
        return result;
    }

    private async Task<SendAsyncResult> SendAsync(string url, CancellationToken cancellationToken)
    {
        using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        linkedSource.CancelAfter(FetchTimeoutMilliseconds);

        var currentUrl = url;
        for (var redirectCount = 0; redirectCount <= MaxRedirects; redirectCount++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, currentUrl);
            request.Headers.UserAgent.ParseAdd("QwenCodeDesktop/0.1");
            request.Headers.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,text/plain;q=0.8,*/*;q=0.7");

            HttpResponseMessage response;
            try
            {
                response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, linkedSource.Token);
            }
            catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException($"Fetching '{url}' timed out after {FetchTimeoutMilliseconds / 1000} seconds.", exception);
            }

            if (!IsRedirectStatusCode(response.StatusCode))
            {
                return new SendAsyncResult(response, currentUrl, null);
            }

            if (response.Headers.Location is not { } location)
            {
                response.Dispose();
                throw new InvalidOperationException($"Redirect response from '{currentUrl}' did not include a Location header.");
            }

            var redirectUrl = BuildRedirectUrl(currentUrl, location);
            if (!IsPermittedRedirect(currentUrl, redirectUrl))
            {
                response.Dispose();
                return new SendAsyncResult(
                    Response: null,
                    EffectiveUrl: currentUrl,
                    CrossHostRedirect: new RedirectInstruction(url, redirectUrl, response.StatusCode));
            }

            response.Dispose();
            currentUrl = redirectUrl;
        }

        throw new InvalidOperationException($"Too many redirects while fetching '{url}'.");
    }

    private static HttpClient CreateDefaultClient()
    {
        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.Brotli | DecompressionMethods.Deflate | DecompressionMethods.GZip
        };

        var client = new HttpClient(handler);
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

        if (!string.IsNullOrWhiteSpace(uri.UserInfo))
        {
            throw new InvalidOperationException("Parameter 'url' must not contain embedded credentials.");
        }

        var builder = new UriBuilder(uri);
        if (builder.Scheme == Uri.UriSchemeHttp)
        {
            builder.Scheme = Uri.UriSchemeHttps;
            if (builder.Port == 80)
            {
                builder.Port = 443;
            }
        }

        var normalized = builder.Uri.ToString();
        if (builder.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase) &&
            builder.Path.Contains("/blob/", StringComparison.OrdinalIgnoreCase))
        {
            return normalized
                .Replace("github.com", "raw.githubusercontent.com", StringComparison.OrdinalIgnoreCase)
                .Replace("/blob/", "/", StringComparison.OrdinalIgnoreCase);
        }

        return normalized;
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
        var promptTerms = Regex.Matches(prompt.ToLowerInvariant(), "[\\p{L}0-9_\\-]{4,}")
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

    private static string BuildDefaultFetchExcerpt(string extractedText)
    {
        var paragraphs = extractedText
            .Split([Environment.NewLine + Environment.NewLine, "\n\n"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(static paragraph => !string.IsNullOrWhiteSpace(paragraph))
            .Take(4)
            .ToArray();

        if (paragraphs.Length == 0)
        {
            return extractedText[..Math.Min(extractedText.Length, 1_500)];
        }

        var result = string.Join(Environment.NewLine + Environment.NewLine, paragraphs);
        return result.Length > 4_000 ? result[..4_000] : result;
    }

    private static bool TryGetCachedFetchResult(string url, out FetchedContentResult result)
    {
        if (FetchCache.TryGetValue(url, out var cached) &&
            cached.ExpiresAtUtc > DateTimeOffset.UtcNow)
        {
            result = cached.Result;
            return true;
        }

        FetchCache.TryRemove(url, out _);
        result = default!;
        return false;
    }

    private static void CacheFetchResult(string url, FetchedContentResult result)
    {
        FetchCache[url] = new CachedFetchResult(result, DateTimeOffset.UtcNow.Add(FetchCacheLifetime));
    }

    private static bool IsRedirectStatusCode(HttpStatusCode statusCode) =>
        statusCode is HttpStatusCode.Moved or
            HttpStatusCode.Redirect or
            HttpStatusCode.RedirectMethod or
            HttpStatusCode.RedirectKeepVerb or
            HttpStatusCode.TemporaryRedirect or
            HttpStatusCode.PermanentRedirect;

    private static string BuildRedirectUrl(string sourceUrl, Uri location) =>
        location.IsAbsoluteUri
            ? location.ToString()
            : new Uri(new Uri(sourceUrl), location).ToString();

    private static bool IsPermittedRedirect(string originalUrl, string redirectUrl)
    {
        if (!Uri.TryCreate(originalUrl, UriKind.Absolute, out var originalUri) ||
            !Uri.TryCreate(redirectUrl, UriKind.Absolute, out var redirectUri))
        {
            return false;
        }

        if (!string.Equals(originalUri.Scheme, redirectUri.Scheme, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var originalPort = originalUri.IsDefaultPort ? GetDefaultPort(originalUri.Scheme) : originalUri.Port;
        var redirectPort = redirectUri.IsDefaultPort ? GetDefaultPort(redirectUri.Scheme) : redirectUri.Port;
        if (originalPort != redirectPort)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(redirectUri.UserInfo))
        {
            return false;
        }

        return string.Equals(
            StripWww(originalUri.Host),
            StripWww(redirectUri.Host),
            StringComparison.OrdinalIgnoreCase);
    }

    private static int GetDefaultPort(string scheme) =>
        string.Equals(scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ? 443 : 80;

    private static string StripWww(string host) =>
        host.StartsWith("www.", StringComparison.OrdinalIgnoreCase)
            ? host[4..]
            : host;

    private static string BuildRedirectMessage(RedirectInstruction redirect, string prompt)
    {
        var promptLine = string.IsNullOrWhiteSpace(prompt)
            ? string.Empty
            : $"{Environment.NewLine}Prompt: {prompt}";

        return $"""
The requested URL redirected to a different host and was not fetched automatically.
Original URL: {redirect.OriginalUrl}
Redirect URL: {redirect.RedirectUrl}
Status: {(int)redirect.StatusCode} {redirect.StatusCode}{promptLine}

Call web_fetch again with the redirect URL if you want to inspect that destination.
""";
    }

    private WebSearchConfiguration? ResolveWebSearchConfiguration(string projectRoot)
    {
        var configSnapshot = new RuntimeConfigService(environmentPaths)
            .Inspect(new WorkspacePaths { WorkspaceRoot = projectRoot });
        var merged = configSnapshot.MergedSettings;
        if (GetNode(merged, "webSearch") is not JsonObject webSearchObject)
        {
            return null;
        }

        var settingsEnvironment = configSnapshot.Environment;
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

    private sealed record RedirectInstruction(
        string OriginalUrl,
        string RedirectUrl,
        HttpStatusCode StatusCode);

    private sealed record FetchedContentResult(
        string EffectiveUrl,
        string ExtractedText,
        RedirectInstruction? CrossHostRedirect);

    private sealed record CachedFetchResult(
        FetchedContentResult Result,
        DateTimeOffset ExpiresAtUtc);

    private sealed record SendAsyncResult(
        HttpResponseMessage? Response,
        string? EffectiveUrl,
        RedirectInstruction? CrossHostRedirect);
}
