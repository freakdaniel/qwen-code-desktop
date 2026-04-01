using QwenCode.App.Enums;
using QwenCode.App.Models;

namespace QwenCode.App.Options;

public sealed class DesktopShellOptions
{
    public const string SectionName = "DesktopShell";

    public string ProductName { get; set; } = "Qwen Code Desktop";

    public string DefaultLocale { get; set; } = "en";

    public DesktopMode DefaultMode { get; set; } = DesktopMode.Chat;

    public SourceMirrorPaths Sources { get; set; } = new();
}
