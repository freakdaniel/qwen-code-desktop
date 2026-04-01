using Microsoft.Extensions.Options;
using QwenCode.App.Enums;
using QwenCode.App.Models;
using QwenCode.App.Options;
using QwenCode.App.Services;

namespace QwenCode.Tests;

public sealed class UnitTest1
{
    [Fact]
    public async Task SetLocaleAsync_FallsBackToKnownLanguageCode()
    {
        var service = CreateService();

        var state = await service.SetLocaleAsync("fr-CA");

        Assert.Equal("fr", state.CurrentLocale);
    }

    [Fact]
    public async Task GetBootstrapAsync_ReturnsConfiguredProductAndSources()
    {
        var expectedSources = new SourceMirrorPaths
        {
            WorkspaceRoot = "E:\\Projects\\qwen-code-desktop",
            QwenRoot = "E:\\Projects\\qwen-code-main",
            ClaudeRoot = "E:\\Projects\\claude-code-main",
            IpcReferenceRoot = "E:\\Projects\\HyPrism"
        };

        var service = CreateService(new DesktopShellOptions
        {
            ProductName = "Qwen Code Desktop",
            DefaultLocale = "ru",
            DefaultMode = DesktopMode.Code,
            Sources = expectedSources
        });

        var payload = await service.GetBootstrapAsync();

        Assert.Equal("Qwen Code Desktop", payload.ProductName);
        Assert.Equal(DesktopMode.Code, payload.CurrentMode);
        Assert.Equal(expectedSources.QwenRoot, payload.Sources.QwenRoot);
        Assert.Contains(payload.Locales, locale => locale.Code == "ar");
    }

    private static DesktopAppService CreateService(DesktopShellOptions? options = null)
        => new(Options.Create(options ?? new DesktopShellOptions()));
}
