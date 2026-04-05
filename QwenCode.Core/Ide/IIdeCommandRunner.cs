namespace QwenCode.App.Ide;

public interface IIdeCommandRunner
{
    Task<IdeCommandResult> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        bool useShellExecute = false,
        CancellationToken cancellationToken = default);
}
