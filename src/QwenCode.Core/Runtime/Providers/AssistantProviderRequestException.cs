namespace QwenCode.Core.Runtime;

/// <summary>
/// Represents a provider request failure that should be surfaced to the session runtime.
/// </summary>
public sealed class AssistantProviderRequestException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AssistantProviderRequestException"/> class.
    /// </summary>
    public AssistantProviderRequestException(
        string providerName,
        string endpoint,
        int? statusCode,
        string responseBody,
        Exception? innerException = null)
        : base(BuildMessage(providerName, endpoint, statusCode, responseBody), innerException)
    {
        ProviderName = providerName;
        Endpoint = endpoint;
        StatusCode = statusCode;
        ResponseBody = responseBody ?? string.Empty;
    }

    /// <summary>
    /// Gets the provider name.
    /// </summary>
    public string ProviderName { get; }

    /// <summary>
    /// Gets the endpoint that failed.
    /// </summary>
    public string Endpoint { get; }

    /// <summary>
    /// Gets the HTTP status code when available.
    /// </summary>
    public int? StatusCode { get; }

    /// <summary>
    /// Gets the response body when available.
    /// </summary>
    public string ResponseBody { get; }

    private static string BuildMessage(string providerName, string endpoint, int? statusCode, string responseBody)
    {
        if (statusCode == 429)
        {
            return $"The model provider '{providerName}' failed with HTTP 429. The provider temporarily rejected the request due to rate limiting, capacity, or quota policy. Please try again later.";
        }

        var trimmedBody = string.IsNullOrWhiteSpace(responseBody)
            ? string.Empty
            : responseBody.Length <= 280
                ? responseBody.Trim()
                : $"{responseBody[..280].Trim()}...";

        var statusSegment = statusCode.HasValue ? $"HTTP {statusCode.Value}" : "request error";
        return string.IsNullOrWhiteSpace(trimmedBody)
            ? $"The model provider '{providerName}' failed with {statusSegment}."
            : $"The model provider '{providerName}' failed with {statusSegment}: {trimmedBody}";
    }
}
