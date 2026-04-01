namespace QwenCode.App.Models;

public sealed class QwenCompatibilitySnapshot
{
    public required string ProjectRoot { get; init; }

    public required string DefaultContextFileName { get; init; }

    public required IReadOnlyList<QwenCompatibilityLayer> SettingsLayers { get; init; }

    public required IReadOnlyList<QwenSurfaceDirectory> SurfaceDirectories { get; init; }

    public required IReadOnlyList<QwenCommandSurface> Commands { get; init; }

    public required IReadOnlyList<QwenSkillSurface> Skills { get; init; }
}
