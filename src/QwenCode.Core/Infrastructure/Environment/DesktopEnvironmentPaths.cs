namespace QwenCode.Core.Infrastructure;

/// <summary>
/// Represents the Desktop Environment Paths
/// </summary>
public sealed class DesktopEnvironmentPaths : IDesktopEnvironmentPaths
{
    /// <summary>
    /// Gets the home directory
    /// </summary>
    public string HomeDirectory => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    /// <summary>
    /// Gets the program data directory
    /// </summary>
    public string? ProgramDataDirectory =>
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

    /// <summary>
    /// Gets the current directory
    /// </summary>
    public string CurrentDirectory => Environment.CurrentDirectory;

    /// <summary>
    /// Gets the app base directory
    /// </summary>
    public string AppBaseDirectory => AppContext.BaseDirectory;
}
