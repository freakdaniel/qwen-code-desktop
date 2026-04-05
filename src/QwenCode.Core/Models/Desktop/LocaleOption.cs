namespace QwenCode.App.Models;

/// <summary>
/// Represents the Locale Option
/// </summary>
public sealed class LocaleOption
{
    /// <summary>
    /// Gets or sets the code
    /// </summary>
    public required string Code { get; init; }

    /// <summary>
    /// Gets or sets the name
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets or sets the native name
    /// </summary>
    public required string NativeName { get; init; }
}
