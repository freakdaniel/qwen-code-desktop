namespace QwenCode.Tests.Runtime;

public sealed class TokenLimitServiceTests
{
    [Theory]
    [InlineData("openai/gpt-5-20250219", "gpt-5")]
    [InlineData("qwen-plus-latest", "qwen-plus-latest")]
    [InlineData("anthropic/claude-sonnet-4-6-preview", "claude-sonnet-4-6")]
    public void TokenLimitService_Normalize_PreservesExpectedModelIdentity(string input, string expected)
    {
        Assert.Equal(expected, TokenLimitService.Normalize(input));
    }

    [Fact]
    public void TokenLimitService_Resolve_ReturnsUpstreamLikeQwenLimits()
    {
        var service = new TokenLimitService();

        var limits = service.Resolve("qwen3-coder-plus", new NativeAssistantRuntimeOptions());

        Assert.Equal(1_000_000, limits.InputTokenLimit);
        Assert.Equal(32_768, limits.OutputTokenLimit);
        Assert.True(limits.HasExplicitOutputLimit);
    }

    [Fact]
    public void TokenLimitService_Resolve_HonorsExplicitOverrides()
    {
        var service = new TokenLimitService();

        var limits = service.Resolve(
            "gpt-5",
            new NativeAssistantRuntimeOptions
            {
                InputTokenLimit = 123_456,
                OutputTokenLimit = 4_096
            });

        Assert.Equal(123_456, limits.InputTokenLimit);
        Assert.Equal(4_096, limits.OutputTokenLimit);
        Assert.True(limits.HasExplicitOutputLimit);
    }
}
