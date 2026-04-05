namespace QwenCode.App.Infrastructure;

public sealed class DesktopEnvironmentPaths : IDesktopEnvironmentPaths
{
    public string HomeDirectory => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    public string? ProgramDataDirectory =>
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

    public string CurrentDirectory => Environment.CurrentDirectory;

    public string AppBaseDirectory => AppContext.BaseDirectory;
}
