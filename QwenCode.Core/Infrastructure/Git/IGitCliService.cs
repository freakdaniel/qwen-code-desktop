namespace QwenCode.App.Infrastructure;

public interface IGitCliService
{
    GitCommandResult Run(string workingDirectory, params string[] arguments);
}
