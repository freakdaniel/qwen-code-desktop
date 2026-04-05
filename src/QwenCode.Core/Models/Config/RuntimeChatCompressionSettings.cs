namespace QwenCode.App.Models;

/// <summary>
/// Represents the Runtime Chat Compression Settings
/// </summary>
public sealed class RuntimeChatCompressionSettings
{
    /// <summary>
    /// Gets or sets the context percentage threshold
    /// </summary>
    public double? ContextPercentageThreshold { get; init; }
}
