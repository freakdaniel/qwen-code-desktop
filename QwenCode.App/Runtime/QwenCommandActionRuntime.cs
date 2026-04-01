using QwenCode.App.Compatibility;
using QwenCode.App.Models;
using QwenCode.App.Tools;

namespace QwenCode.App.Runtime;

public sealed class QwenCommandActionRuntime(
    ISlashCommandRuntime slashCommandRuntime,
    QwenRuntimeProfileService runtimeProfileService,
    QwenCompatibilityService compatibilityService,
    IToolRegistry toolRegistry) : ICommandActionRuntime
{
    private const string BuiltInScope = "built-in";
    private const string MemorySectionHeader = "## Qwen Added Memories";

    public async Task<CommandInvocationResult?> TryInvokeAsync(
        SourceMirrorPaths paths,
        string prompt,
        string workingDirectory,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            return null;
        }

        var trimmedPrompt = prompt.Trim();
        if (!trimmedPrompt.StartsWith("/", StringComparison.Ordinal))
        {
            return null;
        }

        var builtIn = await TryInvokeMemoryCommandAsync(paths, trimmedPrompt, workingDirectory, cancellationToken);
        if (builtIn is not null)
        {
            return builtIn;
        }

        var contextResult = await TryInvokeContextCommandAsync(paths, trimmedPrompt, workingDirectory);
        if (contextResult is not null)
        {
            return contextResult;
        }

        var resolved = slashCommandRuntime.TryResolve(paths, trimmedPrompt, workingDirectory);
        if (resolved is null)
        {
            return null;
        }

        return new CommandInvocationResult
        {
            Command = resolved,
            Status = "resolved",
            IsTerminal = false
        };
    }

    private Task<CommandInvocationResult?> TryInvokeContextCommandAsync(
        SourceMirrorPaths paths,
        string prompt,
        string workingDirectory)
    {
        if (!TryParseContextInvocation(prompt, out var showDetails))
        {
            return Task.FromResult<CommandInvocationResult?>(null);
        }

        var runtimeProfile = runtimeProfileService.Inspect(paths);
        var compatibility = compatibilityService.Inspect(paths);
        var toolCatalog = toolRegistry.Inspect(paths);
        var globalMemoryFiles = ReadMemoryFiles(runtimeProfile.GlobalQwenDirectory, runtimeProfile.ContextFileNames);
        var projectMemoryFiles = ReadMemoryFiles(runtimeProfile.ProjectRoot, runtimeProfile.ContextFileNames);

        var lines = new List<string>
        {
            $"Workspace: {runtimeProfile.ProjectRoot}",
            $"Approval mode: {runtimeProfile.ApprovalProfile.DefaultMode}",
            $"Context files: {runtimeProfile.ContextFileNames.Count}",
            $"Memory files: {globalMemoryFiles.Count + projectMemoryFiles.Count}",
            $"Slash commands: {compatibility.Commands.Count}",
            $"Skills: {compatibility.Skills.Count}",
            $"Tool catalog: {toolCatalog.TotalCount} (allow {toolCatalog.AllowedCount}, ask {toolCatalog.AskCount}, deny {toolCatalog.DenyCount})"
        };

        if (showDetails)
        {
            lines.Add(string.Empty);
            lines.Add("Context file names:");
            lines.AddRange(runtimeProfile.ContextFileNames.Select(item => $"- {item}"));

            lines.Add(string.Empty);
            lines.Add("Memory files:");
            lines.AddRange(globalMemoryFiles.Concat(projectMemoryFiles).Select(item => $"- {item.Path}"));

            lines.Add(string.Empty);
            lines.Add("Slash commands:");
            lines.AddRange(compatibility.Commands.Select(item => $"- {item.Scope}:{item.Name}"));

            lines.Add(string.Empty);
            lines.Add("Skills:");
            lines.AddRange(compatibility.Skills.Select(item => $"- {item.Scope}:{item.Name}"));
        }

        return Task.FromResult<CommandInvocationResult?>(
            new CommandInvocationResult
            {
                Command = CreateBuiltInCommand("context", showDetails ? "detail" : string.Empty, null, "Show context window usage breakdown."),
                Status = "completed",
                Output = string.Join(Environment.NewLine, lines),
                IsTerminal = true
            });
    }

    private async Task<CommandInvocationResult?> TryInvokeMemoryCommandAsync(
        SourceMirrorPaths paths,
        string prompt,
        string workingDirectory,
        CancellationToken cancellationToken)
    {
        if (!TryParseMemoryInvocation(prompt, out var action, out var scope, out var payload))
        {
            return null;
        }

        var runtimeProfile = runtimeProfileService.Inspect(paths);
        return action switch
        {
            "show" => await ShowMemoryAsync(runtimeProfile, scope, payload),
            "refresh" => await RefreshMemoryAsync(runtimeProfile),
            "add" => await AddMemoryAsync(runtimeProfile, scope, payload, cancellationToken),
            _ => CreateError("memory", scope, payload, "Unsupported memory subcommand.")
        };
    }

    private static Task<CommandInvocationResult> ShowMemoryAsync(QwenRuntimeProfile runtimeProfile, string? scope, string payload)
    {
        var files = scope switch
        {
            "project" => ReadMemoryFiles(runtimeProfile.ProjectRoot, runtimeProfile.ContextFileNames),
            "global" => ReadMemoryFiles(runtimeProfile.GlobalQwenDirectory, runtimeProfile.ContextFileNames),
            _ => [.. ReadMemoryFiles(runtimeProfile.GlobalQwenDirectory, runtimeProfile.ContextFileNames), .. ReadMemoryFiles(runtimeProfile.ProjectRoot, runtimeProfile.ContextFileNames)]
        };

        if (files.Count == 0)
        {
            return Task.FromResult(new CommandInvocationResult
            {
                Command = CreateBuiltInCommand(
                    scope is null ? "memory/show" : $"memory/show/{scope}",
                    payload,
                    scope,
                    "Show the current memory contents."),
                Status = "completed",
                Output = "Memory is currently empty.",
                IsTerminal = true
            });
        }

        var output = string.Join(
            Environment.NewLine + Environment.NewLine,
            files.Select(file => $"{file.Path}:{Environment.NewLine}---{Environment.NewLine}{file.Content}{Environment.NewLine}---"));

        return Task.FromResult(new CommandInvocationResult
        {
            Command = CreateBuiltInCommand(
                scope is null ? "memory/show" : $"memory/show/{scope}",
                payload,
                scope,
                "Show the current memory contents."),
            Status = "completed",
            Output = output,
            IsTerminal = true
        });
    }

    private static Task<CommandInvocationResult> RefreshMemoryAsync(QwenRuntimeProfile runtimeProfile)
    {
        var files = ReadMemoryFiles(runtimeProfile.GlobalQwenDirectory, runtimeProfile.ContextFileNames);
        files.AddRange(ReadMemoryFiles(runtimeProfile.ProjectRoot, runtimeProfile.ContextFileNames));
        var output = files.Count > 0
            ? $"Memory refreshed successfully. Loaded {files.Count} file(s)."
            : "Memory refreshed successfully. No memory content found.";

        return Task.FromResult(new CommandInvocationResult
        {
            Command = CreateBuiltInCommand("memory/refresh", string.Empty, null, "Refresh the memory from source files."),
            Status = "completed",
            Output = output,
            IsTerminal = true
        });
    }

    private static async Task<CommandInvocationResult> AddMemoryAsync(
        QwenRuntimeProfile runtimeProfile,
        string? scope,
        string payload,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return CreateError("memory/add", scope, payload, "Usage: /memory add [--global|--project] <text to remember>");
        }

        if (scope is null)
        {
            return CreateError("memory/add", scope, payload, "Explicit scope is required for now. Use /memory add --project ... or /memory add --global ...");
        }

        var targetRoot = scope == "global" ? runtimeProfile.GlobalQwenDirectory : runtimeProfile.ProjectRoot;
        var targetFile = Path.Combine(targetRoot, runtimeProfile.ContextFileNames[0]);
        Directory.CreateDirectory(Path.GetDirectoryName(targetFile)!);

        var currentContent = File.Exists(targetFile)
            ? await File.ReadAllTextAsync(targetFile, cancellationToken)
            : string.Empty;
        var updatedContent = AppendMemory(currentContent, payload.Trim());
        await File.WriteAllTextAsync(targetFile, updatedContent, cancellationToken);

        return new CommandInvocationResult
        {
            Command = CreateBuiltInCommand($"memory/add/{scope}", payload, scope, "Add content to memory."),
            Status = "completed",
            Output = $"Saved memory to {targetFile}",
            IsTerminal = true
        };
    }

    private static CommandInvocationResult CreateError(string name, string? scope, string arguments, string message) =>
        new()
        {
            Command = CreateBuiltInCommand(name, arguments, scope, "Built-in memory command."),
            Status = "error",
            ErrorMessage = message,
            IsTerminal = true
        };

    private static QwenResolvedCommand CreateBuiltInCommand(
        string name,
        string arguments,
        string? scope,
        string description) =>
        new()
        {
            Name = name,
            Scope = scope ?? BuiltInScope,
            SourcePath = $"built-in://{name}",
            Description = description,
            Arguments = arguments,
            ResolvedPrompt = string.Empty
        };

    private static string AppendMemory(string currentContent, string fact)
    {
        if (string.IsNullOrWhiteSpace(currentContent))
        {
            return $"{MemorySectionHeader}{Environment.NewLine}- {fact}{Environment.NewLine}";
        }

        var normalized = currentContent.Replace("\r\n", "\n", StringComparison.Ordinal);
        if (!normalized.Contains(MemorySectionHeader, StringComparison.Ordinal))
        {
            var separator = normalized.EndsWith('\n') ? string.Empty : Environment.NewLine + Environment.NewLine;
            return $"{currentContent}{separator}{MemorySectionHeader}{Environment.NewLine}- {fact}{Environment.NewLine}";
        }

        var insertion = $"{Environment.NewLine}- {fact}";
        return currentContent.EndsWith(Environment.NewLine, StringComparison.Ordinal)
            ? currentContent + $"- {fact}{Environment.NewLine}"
            : currentContent + insertion + Environment.NewLine;
    }

    private static List<(string Path, string Content)> ReadMemoryFiles(string root, IReadOnlyList<string> fileNames)
    {
        var results = new List<(string Path, string Content)>();
        foreach (var fileName in fileNames)
        {
            var path = Path.Combine(root, fileName);
            try
            {
                if (!File.Exists(path))
                {
                    continue;
                }

                var content = File.ReadAllText(path);
                if (!string.IsNullOrWhiteSpace(content))
                {
                    results.Add((path, content.Trim()));
                }
            }
            catch
            {
                // Ignore unreadable memory files in discovery/refresh flows.
            }
        }

        return results;
    }

    private static bool TryParseMemoryInvocation(
        string prompt,
        out string action,
        out string? scope,
        out string payload)
    {
        action = string.Empty;
        scope = null;
        payload = string.Empty;

        var parts = prompt.Trim().Split(' ', 4, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0 || !string.Equals(parts[0], "/memory", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        action = parts.Length > 1 ? parts[1].Trim().ToLowerInvariant() : "show";
        if (action is not "show" and not "add" and not "refresh")
        {
            return false;
        }

        if (action == "refresh")
        {
            return true;
        }

        var remaining = parts.Length > 2
            ? string.Join(' ', parts.Skip(2))
            : string.Empty;
        if (remaining.StartsWith("--project ", StringComparison.OrdinalIgnoreCase))
        {
            scope = "project";
            payload = remaining["--project ".Length..].Trim();
            return true;
        }

        if (string.Equals(remaining, "--project", StringComparison.OrdinalIgnoreCase))
        {
            scope = "project";
            return true;
        }

        if (remaining.StartsWith("--global ", StringComparison.OrdinalIgnoreCase))
        {
            scope = "global";
            payload = remaining["--global ".Length..].Trim();
            return true;
        }

        if (string.Equals(remaining, "--global", StringComparison.OrdinalIgnoreCase))
        {
            scope = "global";
            return true;
        }

        payload = remaining;
        return true;
    }

    private static bool TryParseContextInvocation(string prompt, out bool showDetails)
    {
        showDetails = false;
        var parts = prompt.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0 || !string.Equals(parts[0], "/context", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        showDetails = parts.Length > 1 &&
                      (string.Equals(parts[1], "detail", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(parts[1], "-d", StringComparison.OrdinalIgnoreCase));
        return true;
    }
}
