using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace QwenCode.Core.Runtime;

internal static class AssistantProviderRetryPolicy
{
    private const int MaxRetryAttempts = 7;
    private const double JitterRatio = 0.3;
    private static readonly TimeSpan InitialDelay = TimeSpan.FromMilliseconds(1500);
    private static readonly TimeSpan MaximumDelay = TimeSpan.FromSeconds(30);

    public static int MaxAttempts => MaxRetryAttempts;

    public static TimeSpan FirstDelay => InitialDelay;

    public static bool ShouldRetry(string authType, HttpStatusCode statusCode, string responseBody)
    {
        var numericStatus = (int)statusCode;
        var isTransient = numericStatus == 429 || (numericStatus >= 500 && numericStatus < 600);
        if (!isTransient)
        {
            return false;
        }

        return numericStatus != 429 || !IsQwenQuotaExceeded(authType, responseBody);
    }

    public static bool TryGetRetryAfterDelay(HttpResponseHeaders headers, out TimeSpan delay)
    {
        if (headers.RetryAfter?.Delta is TimeSpan delta)
        {
            delay = delta < TimeSpan.Zero ? TimeSpan.Zero : delta;
            return true;
        }

        if (headers.RetryAfter?.Date is DateTimeOffset date)
        {
            delay = date - DateTimeOffset.UtcNow;
            if (delay < TimeSpan.Zero)
            {
                delay = TimeSpan.Zero;
            }

            return true;
        }

        if (headers.TryGetValues("Retry-After", out var values))
        {
            foreach (var value in values)
            {
                if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds))
                {
                    delay = TimeSpan.FromSeconds(Math.Max(0, seconds));
                    return true;
                }

                if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsedDate))
                {
                    delay = parsedDate - DateTimeOffset.UtcNow;
                    if (delay < TimeSpan.Zero)
                    {
                        delay = TimeSpan.Zero;
                    }

                    return true;
                }
            }
        }

        delay = TimeSpan.Zero;
        return false;
    }

    public static TimeSpan ApplyBackoff(TimeSpan currentDelay)
    {
        var jitter = 1 + ((Random.Shared.NextDouble() * 2 - 1) * JitterRatio);
        var jitteredDelay = TimeSpan.FromMilliseconds(Math.Max(0, currentDelay.TotalMilliseconds * jitter));
        return jitteredDelay;
    }

    public static TimeSpan GetNextDelay(TimeSpan currentDelay)
    {
        var nextDelay = TimeSpan.FromMilliseconds(currentDelay.TotalMilliseconds * 2);
        return nextDelay > MaximumDelay ? MaximumDelay : nextDelay;
    }

    private static bool IsQwenQuotaExceeded(string authType, string responseBody)
    {
        if (!string.Equals(authType, "qwen-oauth", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(authType, "qwen_oauth", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(responseBody);
            var root = document.RootElement;
            var errorElement = root.TryGetProperty("error", out var nestedError) &&
                               nestedError.ValueKind == JsonValueKind.Object
                ? nestedError
                : root;

            var code = errorElement.TryGetProperty("code", out var codeProperty) &&
                       codeProperty.ValueKind == JsonValueKind.String
                ? codeProperty.GetString()
                : string.Empty;
            var message = errorElement.TryGetProperty("message", out var messageProperty) &&
                          messageProperty.ValueKind == JsonValueKind.String
                ? messageProperty.GetString()
                : string.Empty;
            code ??= string.Empty;
            message ??= string.Empty;

            return string.Equals(code, "insufficient_quota", StringComparison.OrdinalIgnoreCase) &&
                   message.Contains("free allocated quota exceeded", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }
}
