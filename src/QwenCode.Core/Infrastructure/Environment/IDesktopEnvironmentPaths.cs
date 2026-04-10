namespace QwenCode.Core.Infrastructure;

/// <summary>
/// Defines the contract for Desktop Environment Paths
/// </summary>
public interface IDesktopEnvironmentPaths
{
    /// <summary>
    /// Gets the home directory
    /// </summary>
    string HomeDirectory { get; }

    /// <summary>
    /// Gets the program data directory
    /// </summary>
    string? ProgramDataDirectory { get; }

    /// <summary>
    /// Gets the current directory
    /// </summary>
    string CurrentDirectory { get; }

    /// <summary>
    /// Gets the app base directory
    /// </summary>
    string AppBaseDirectory { get; }
}
