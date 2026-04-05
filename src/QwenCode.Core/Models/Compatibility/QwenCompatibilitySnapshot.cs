namespace QwenCode.App.Models;

/// <summary>
/// Represents the Qwen Compatibility Snapshot
/// </summary>
public sealed class QwenCompatibilitySnapshot
{
    /// <summary>
    /// Gets or sets the project root
    /// </summary>
    public required string ProjectRoot { get; init; }

    /// <summary>
    /// Gets or sets the default context file name
    /// </summary>
    public required string DefaultContextFileName { get; init; }

    /// <summary>
    /// Gets or sets the settings layers
    /// </summary>
    public required IReadOnlyList<QwenCompatibilityLayer> SettingsLayers { get; init; }

    /// <summary>
    /// Gets or sets the surface directories
    /// </summary>
    public required IReadOnlyList<QwenSurfaceDirectory> SurfaceDirectories { get; init; }

    /// <summary>
    /// Gets or sets the commands
    /// </summary>
    public required IReadOnlyList<QwenCommandSurface> Commands { get; init; }

    /// <summary>
    /// Gets or sets the skills
    /// </summary>
    public required IReadOnlyList<QwenSkillSurface> Skills { get; init; }
}
