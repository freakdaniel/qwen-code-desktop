using QwenCode.App.Models;

namespace QwenCode.App.Ide;

public interface IIdeDetectionService
{
    IdeInfo? Detect(string processCommand, IReadOnlyDictionary<string, string>? environment = null, IdeInfo? overrideInfo = null);
}
