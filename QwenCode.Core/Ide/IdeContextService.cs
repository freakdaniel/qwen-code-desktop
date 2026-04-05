using QwenCode.App.Models;

namespace QwenCode.App.Ide;

public sealed class IdeContextService : IIdeContextService
{
    internal const int MaxOpenFiles = 10;
    internal const int MaxSelectedTextLength = 16_384;

    private readonly Lock gate = new();
    private IdeContextSnapshot? snapshot;

    public IdeContextSnapshot Normalize(IdeContextSnapshot input)
    {
        var openFiles = input.OpenFiles
            .OrderByDescending(static item => item.Timestamp)
            .Take(MaxOpenFiles)
            .ToArray();

        if (openFiles.Length > 0)
        {
            var activeIndex = Array.FindIndex(openFiles, static item => item.IsActive);
            if (activeIndex < 0)
            {
                for (var index = 0; index < openFiles.Length; index++)
                {
                    openFiles[index] = ClearActiveDetails(openFiles[index]);
                }
            }
            else
            {
                for (var index = 0; index < openFiles.Length; index++)
                {
                    if (index == activeIndex)
                    {
                        openFiles[index] = NormalizeActive(openFiles[index]);
                    }
                    else
                    {
                        openFiles[index] = ClearActiveDetails(openFiles[index]);
                    }
                }
            }
        }

        return new IdeContextSnapshot
        {
            OpenFiles = openFiles,
            IsTrusted = input.IsTrusted
        };
    }

    public void Set(IdeContextSnapshot input)
    {
        lock (gate)
        {
            snapshot = Normalize(input);
        }
    }

    public IdeContextSnapshot? Get()
    {
        lock (gate)
        {
            return snapshot;
        }
    }

    public void Clear()
    {
        lock (gate)
        {
            snapshot = null;
        }
    }

    private static IdeOpenFile NormalizeActive(IdeOpenFile file) =>
        new()
        {
            Path = file.Path,
            Timestamp = file.Timestamp,
            IsActive = true,
            Cursor = file.Cursor,
            SelectedText = string.IsNullOrEmpty(file.SelectedText)
                ? string.Empty
                : file.SelectedText.Length <= MaxSelectedTextLength
                    ? file.SelectedText
                    : file.SelectedText[..MaxSelectedTextLength] + "... [TRUNCATED]"
        };

    private static IdeOpenFile ClearActiveDetails(IdeOpenFile file) =>
        new()
        {
            Path = file.Path,
            Timestamp = file.Timestamp,
            IsActive = false,
            Cursor = null,
            SelectedText = string.Empty
        };
}
