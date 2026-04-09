using QwenCode.App.Desktop.Projection;

namespace QwenCode.Tests.Sessions;

public sealed class SessionTitleGenerationServiceTests
{
    [Fact]
    public void ResolveLanguageName_KnownLocale_ReturnsCorrectName()
    {
        Assert.Equal("Russian", SessionTitleGenerationService.ResolveLanguageName("ru"));
        Assert.Equal("Russian", SessionTitleGenerationService.ResolveLanguageName("ru-RU"));
        Assert.Equal("Chinese", SessionTitleGenerationService.ResolveLanguageName("zh"));
        Assert.Equal("Chinese", SessionTitleGenerationService.ResolveLanguageName("zh-CN"));
        Assert.Equal("Japanese", SessionTitleGenerationService.ResolveLanguageName("ja"));
        Assert.Equal("Japanese", SessionTitleGenerationService.ResolveLanguageName("ja-JP"));
        Assert.Equal("Korean", SessionTitleGenerationService.ResolveLanguageName("ko"));
        Assert.Equal("Korean", SessionTitleGenerationService.ResolveLanguageName("ko-KR"));
        Assert.Equal("Portuguese", SessionTitleGenerationService.ResolveLanguageName("pt"));
        Assert.Equal("Portuguese", SessionTitleGenerationService.ResolveLanguageName("pt-BR"));
        Assert.Equal("English", SessionTitleGenerationService.ResolveLanguageName("en"));
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

    [Fact]
    public void BuildTitleSystemPrompt_UsesLocaleAndLanguageConstraints()
    {
        var prompt = SessionTitleGenerationService.BuildTitleSystemPrompt("ru-RU");

        Assert.Contains("Reply in Russian", prompt, StringComparison.Ordinal);
        Assert.Contains("The application UI locale is ru-RU", prompt, StringComparison.Ordinal);
        Assert.Contains("never more than 7 words", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Return only the title", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void NormalizeGeneratedTitle_StripsQuotesPunctuationAndExtraWords()
    {
        var normalized = SessionTitleGenerationService.NormalizeGeneratedTitle("  \"Fix Avalonia Scrollbar Rendering Issues.\"  ");
        Assert.Equal("Fix Avalonia Scrollbar Rendering Issues", normalized);
    }

    [Fact]
    public void NormalizeGeneratedTitle_KeepsAtMostSevenWords()
    {
        var normalized = SessionTitleGenerationService.NormalizeGeneratedTitle("One two three four five six seven eight nine");
        Assert.Equal("One two three four five six seven", normalized);
    }
}
