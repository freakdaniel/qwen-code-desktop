namespace QwenCode.Tests.Shared.Fakes;

internal sealed class FakeDesktopEnvironmentPaths(
    string homeDirectory,
    string? programDataDirectory,
    string? currentDirectory = null,
    string? appBaseDirectory = null)
    : IDesktopEnvironmentPaths
{
    public string HomeDirectory { get; } = homeDirectory;

    public string? ProgramDataDirectory { get; } = programDataDirectory;

    public string CurrentDirectory { get; } = currentDirectory ?? homeDirectory;

    public string AppBaseDirectory { get; } = appBaseDirectory ?? homeDirectory;
}
