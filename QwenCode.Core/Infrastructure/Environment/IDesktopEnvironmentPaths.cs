namespace QwenCode.App.Infrastructure;

public interface IDesktopEnvironmentPaths
{
    string HomeDirectory { get; }

    string? ProgramDataDirectory { get; }

    string CurrentDirectory { get; }

    string AppBaseDirectory { get; }
}
