using QwenCode.App.Desktop.Projection;

namespace QwenCode.Tests.Sessions;

public sealed class SessionTitleGenerationServiceTests
{
    [Fact]
    public void ResolveLanguageName_KnownLocale_ReturnsCorrectName()
    {
        Assert.Equal("Russian", SessionTitleGenerationService.ResolveLanguageName("ru-RU"));
        Assert.Equal("Chinese", SessionTitleGenerationService.ResolveLanguageName("zh-CN"));
        Assert.Equal("Japanese", SessionTitleGenerationService.ResolveLanguageName("ja-JP"));
        Assert.Equal("Korean", SessionTitleGenerationService.ResolveLanguageName("ko-KR"));
        Assert.Equal("Portuguese", SessionTitleGenerationService.ResolveLanguageName("pt-BR"));
        Assert.Equal("English", SessionTitleGenerationService.ResolveLanguageName("en-US"));
    }

    [Fact]
    public void ResolveLanguageName_UnknownLocale_FallsBackToEnglish()
    {
        Assert.Equal("English", SessionTitleGenerationService.ResolveLanguageName("xx-YY"));
        Assert.Equal("English", SessionTitleGenerationService.ResolveLanguageName(""));
    }

    [Fact]
    public void BuildFallbackTitle_ShortText_ReturnsAsIs()
    {
        var result = SessionTitleGenerationService.BuildFallbackTitle("Fix bug");
        Assert.Equal("Fix bug", result);
    }

    [Fact]
    public void BuildFallbackTitle_LongText_TruncatesAt60WithEllipsis()
    {
        var longText = new string('a', 80);
        var result = SessionTitleGenerationService.BuildFallbackTitle(longText);
        Assert.Equal(63, result.Length); // 60 chars + "..."
        Assert.EndsWith("...", result);
    }
}
