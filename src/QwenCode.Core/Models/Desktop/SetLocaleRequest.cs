namespace QwenCode.Core.Models;

/// <summary>
/// Represents the Set Locale Request
/// </summary>
public sealed class SetLocaleRequest
{
    /// <summary>
    /// Gets or sets the locale
    /// </summary>
    public required string Locale { get; init; }
}
