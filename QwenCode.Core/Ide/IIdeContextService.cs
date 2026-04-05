using QwenCode.App.Models;

namespace QwenCode.App.Ide;

public interface IIdeContextService
{
    IdeContextSnapshot Normalize(IdeContextSnapshot snapshot);

    void Set(IdeContextSnapshot snapshot);

    IdeContextSnapshot? Get();

    void Clear();
}
