namespace QwenCode.Core.Runtime;

internal static class NativeAssistantPromptSectionResolver
{
    public static IReadOnlyList<NativeAssistantPromptSection> ResolveSystemSections(
        NativeAssistantPromptCompositionContext context,
        bool includeDynamicSections = true) =>
        NativeAssistantPromptSectionRegistry.SystemSections
            .Where(section => includeDynamicSections || !section.IsDynamic)
            .Where(section => section.AppliesTo(context))
            .OrderBy(section => section.Order)
            .ThenBy(section => section.Name, StringComparer.Ordinal)
            .ToArray();

    public static IReadOnlyList<NativeAssistantPromptSection> ResolveStaticSystemSections(
        NativeAssistantPromptCompositionContext context) =>
        ResolveSystemSections(context, includeDynamicSections: false);

    public static IReadOnlyList<NativeAssistantPromptSection> ResolveDynamicSystemSections(
        NativeAssistantPromptCompositionContext context) =>
        NativeAssistantPromptSectionRegistry.SystemSections
            .Where(section => section.IsDynamic)
            .Where(section => section.AppliesTo(context))
            .OrderBy(section => section.Order)
            .ThenBy(section => section.Name, StringComparer.Ordinal)
            .ToArray();
}
