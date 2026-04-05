namespace QwenCode.Tests.Agents;

public sealed class SubagentModelSelectionAndValidationTests
{
    [Fact]
    public void SubagentModelSelectionService_Parse_SupportsExplicitAuthPrefix()
    {
        var service = new SubagentModelSelectionService();

        var selection = service.Parse("qwen-compatible:qwen3-coder-plus", "openai");

        Assert.False(selection.Inherits);
        Assert.Equal("qwen-compatible", selection.AuthType);
        Assert.Equal("qwen3-coder-plus", selection.ModelId);
    }

    [Fact]
    public void SubagentModelSelectionService_Parse_TreatsUnknownPrefixAsBareModel()
    {
        var service = new SubagentModelSelectionService();

        var selection = service.Parse("gpt-4o:online", "openai");

        Assert.False(selection.Inherits);
        Assert.Equal("openai", selection.AuthType);
        Assert.Equal("gpt-4o:online", selection.ModelId);
    }

    [Fact]
    public void SubagentValidationService_Validate_ReturnsErrorsForInvalidDescriptor()
    {
        var validation = new SubagentValidationService(new SubagentModelSelectionService())
            .Validate(new SubagentDescriptor
            {
                Name = "_",
                Description = string.Empty,
                SystemPrompt = "short",
                RunConfiguration = new SubagentRunConfiguration
                {
                    MaxTurns = 0
                },
                Tools = ["", "read_file"]
            });

        Assert.False(validation.IsValid);
        Assert.NotEmpty(validation.Errors);
    }
}
