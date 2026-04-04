using QwenCode.App.Options;

namespace QwenCode.App.Runtime;

public interface ITokenLimitService
{
    ResolvedTokenLimits Resolve(string model, NativeAssistantRuntimeOptions options);
}
