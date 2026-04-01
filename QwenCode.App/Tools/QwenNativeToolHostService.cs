using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using QwenCode.App.Models;
using QwenCode.App.Compatibility;
using QwenCode.App.Permissions;

namespace QwenCode.App.Tools;

public sealed class QwenNativeToolHostService(
    QwenRuntimeProfileService runtimeProfileService,
    IApprovalPolicyEngine approvalPolicyService) : IToolExecutor
{
    private static readonly IReadOnlyDictionary<string, (string DisplayName, string Kind)> NativeTools =
        new Dictionary<string, (string DisplayName, string Kind)>(StringComparer.OrdinalIgnoreCase)
        {
            ["read_file"] = ("ReadFile", "read"),
            ["list_directory"] = ("ListFiles", "read"),
            ["glob"] = ("Glob", "read"),
            ["grep_search"] = ("Grep", "read"),
            ["run_shell_command"] = ("Shell", "execute"),
            ["write_file"] = ("WriteFile", "modify"),
            ["edit"] = ("Edit", "modify")
        };

    private static readonly string[] IgnoredDirectories = [".git", "node_modules", "bin", "obj", ".electron", "dist"];

    public QwenNativeToolHostSnapshot Inspect(SourceMirrorPaths paths)
    {
        var runtimeProfile = runtimeProfileService.Inspect(paths);
        var registrations = NativeTools
            .Select(tool =>
            {
                var approval = approvalPolicyService.Evaluate(
                    new ApprovalCheckContext
                    {
                        ToolName = tool.Key,
                        Kind = tool.Value.Kind,
                        ProjectRoot = runtimeProfile.ProjectRoot,
                        WorkingDirectory = runtimeProfile.ProjectRoot
                    },
                    runtimeProfile.ApprovalProfile);
                return new QwenNativeToolRegistration
                {
                    Name = tool.Key,
                    DisplayName = tool.Value.DisplayName,
                    Kind = tool.Value.Kind,
                    IsImplemented = true,
                    ApprovalState = approval.State,
                    ApprovalReason = approval.Reason
                };
            })
            .OrderBy(static tool => tool.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new QwenNativeToolHostSnapshot
        {
            RegisteredCount = registrations.Length,
            ImplementedCount = registrations.Count(static tool => tool.IsImplemented),
            ReadyCount = registrations.Count(static tool => tool.ApprovalState == "allow"),
            ApprovalRequiredCount = registrations.Count(static tool => tool.ApprovalState == "ask"),
            Tools = registrations
        };
    }

    public async Task<QwenNativeToolExecutionResult> ExecuteAsync(
        SourceMirrorPaths paths,
        ExecuteNativeToolRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!NativeTools.TryGetValue(request.ToolName, out var tool))
        {
            return Error(request.ToolName, "Tool is not implemented by the native .NET host yet.", paths.WorkspaceRoot);
        }

        var runtimeProfile = runtimeProfileService.Inspect(paths);

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(string.IsNullOrWhiteSpace(request.ArgumentsJson) ? "{}" : request.ArgumentsJson);
        }
        catch (Exception exception)
        {
            return Error(request.ToolName, $"Failed to parse tool arguments: {exception.Message}", runtimeProfile.ProjectRoot);
        }

        using (document)
        {
            var approvalContext = BuildApprovalContext(request.ToolName, tool.Kind, runtimeProfile, document.RootElement);
            var approval = approvalPolicyService.Evaluate(approvalContext, runtimeProfile.ApprovalProfile);
            if (approval.State == "deny")
            {
                return new QwenNativeToolExecutionResult
                {
                    ToolName = request.ToolName,
                    Status = "blocked",
                    ApprovalState = approval.State,
                    WorkingDirectory = approvalContext.WorkingDirectory ?? runtimeProfile.ProjectRoot,
                    ErrorMessage = approval.Reason,
                    ChangedFiles = []
                };
            }

            if (approval.State == "ask" && !request.ApproveExecution)
            {
                return new QwenNativeToolExecutionResult
                {
                    ToolName = request.ToolName,
                    Status = "approval-required",
                    ApprovalState = approval.State,
                    WorkingDirectory = approvalContext.WorkingDirectory ?? runtimeProfile.ProjectRoot,
                    ErrorMessage = approval.Reason,
                    ChangedFiles = []
                };
            }

            return request.ToolName switch
            {
                "read_file" => await ExecuteReadFileAsync(runtimeProfile, document.RootElement, approval.State, cancellationToken),
                "list_directory" => ExecuteListDirectory(runtimeProfile, document.RootElement, approval.State),
                "glob" => ExecuteGlob(runtimeProfile, document.RootElement, approval.State),
                "grep_search" => ExecuteGrep(runtimeProfile, document.RootElement, approval.State),
                "run_shell_command" => await ExecuteShellAsync(runtimeProfile, document.RootElement, approval.State, cancellationToken),
                "write_file" => await ExecuteWriteFileAsync(runtimeProfile, document.RootElement, approval.State, cancellationToken),
                "edit" => await ExecuteEditAsync(runtimeProfile, document.RootElement, approval.State, cancellationToken),
                _ => Error(request.ToolName, "Tool is not implemented by the native .NET host yet.", runtimeProfile.ProjectRoot)
            };
        }
    }

    private static ApprovalCheckContext BuildApprovalContext(
        string toolName,
        string kind,
        QwenRuntimeProfile runtimeProfile,
        JsonElement arguments)
    {
        var workingDirectory = TryGetString(arguments, "directory", out var directory)
            ? ResolvePath(directory, runtimeProfile.ProjectRoot)
            : TryGetString(arguments, "path", out var explicitPath) && Directory.Exists(ResolvePath(explicitPath, runtimeProfile.ProjectRoot))
                ? ResolvePath(explicitPath, runtimeProfile.ProjectRoot)
                : runtimeProfile.ProjectRoot;

        var domain = TryExtractDomain(arguments);

        return new ApprovalCheckContext
        {
            ToolName = toolName,
            Kind = kind,
            ProjectRoot = runtimeProfile.ProjectRoot,
            WorkingDirectory = workingDirectory,
            Command = TryGetString(arguments, "command", out var command) ? command : null,
            FilePath = TryExtractFilePath(arguments, runtimeProfile.ProjectRoot),
            Domain = domain,
            Specifier = TryGetString(arguments, "agent_type", out var agentType)
                ? agentType
                : TryGetString(arguments, "skill_name", out var skillName)
                    ? skillName
                    : null
        };
    }

    private static string? TryExtractFilePath(JsonElement arguments, string workspaceRoot)
    {
        if (TryGetString(arguments, "file_path", out var filePath))
        {
            return ResolvePath(filePath, workspaceRoot);
        }

        if (TryGetString(arguments, "path", out var pathValue))
        {
            return ResolvePath(pathValue, workspaceRoot);
        }

        return null;
    }

    private static string? TryExtractDomain(JsonElement arguments)
    {
        if (!TryGetString(arguments, "url", out var url))
        {
            return null;
        }

        return Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.Host : null;
    }

    private static async Task<QwenNativeToolExecutionResult> ExecuteReadFileAsync(
        QwenRuntimeProfile runtimeProfile,
        JsonElement arguments,
        string approvalState,
        CancellationToken cancellationToken)
    {
        var filePath = RequirePath(arguments, "file_path", runtimeProfile.ProjectRoot, absoluteOnly: true);
        if (filePath.IsError)
        {
            return Error("read_file", filePath.ErrorMessage!, runtimeProfile.ProjectRoot, approvalState);
        }

        if (!File.Exists(filePath.Value))
        {
            return Error("read_file", "File not found.", runtimeProfile.ProjectRoot, approvalState);
        }

        var content = await File.ReadAllTextAsync(filePath.Value!, cancellationToken);
        var lines = content.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var offset = TryGetInt(arguments, "offset") ?? 0;
        var limit = TryGetInt(arguments, "limit") ?? lines.Length;
        offset = Math.Max(0, offset);
        limit = Math.Max(1, limit);
        var slice = lines.Skip(offset).Take(limit).ToArray();

        return Success(
            "read_file",
            approvalState,
            runtimeProfile.ProjectRoot,
            string.Join(Environment.NewLine, slice),
            []);
    }

    private static QwenNativeToolExecutionResult ExecuteListDirectory(
        QwenRuntimeProfile runtimeProfile,
        JsonElement arguments,
        string approvalState)
    {
        var directoryPath = RequirePath(arguments, "path", runtimeProfile.ProjectRoot, absoluteOnly: true);
        if (directoryPath.IsError)
        {
            return Error("list_directory", directoryPath.ErrorMessage!, runtimeProfile.ProjectRoot, approvalState);
        }

        if (!Directory.Exists(directoryPath.Value))
        {
            return Error("list_directory", "Directory not found.", runtimeProfile.ProjectRoot, approvalState);
        }

        var entries = Directory.EnumerateFileSystemEntries(directoryPath.Value!)
            .Where(static path => !IgnoredDirectories.Contains(Path.GetFileName(path), StringComparer.OrdinalIgnoreCase))
            .Take(100)
            .Select(static path =>
            {
                var isDirectory = Directory.Exists(path);
                return $"{(isDirectory ? "[DIR]" : "[FILE]")} {path}";
            })
            .ToArray();

        return Success("list_directory", approvalState, runtimeProfile.ProjectRoot, string.Join(Environment.NewLine, entries), []);
    }

    private static QwenNativeToolExecutionResult ExecuteGlob(
        QwenRuntimeProfile runtimeProfile,
        JsonElement arguments,
        string approvalState)
    {
        if (!TryGetString(arguments, "pattern", out var pattern))
        {
            return Error("glob", "Parameter 'pattern' is required.", runtimeProfile.ProjectRoot, approvalState);
        }

        var searchRoot = TryGetString(arguments, "path", out var pathValue)
            ? ResolvePath(pathValue, runtimeProfile.ProjectRoot)
            : runtimeProfile.ProjectRoot;
        if (!Directory.Exists(searchRoot))
        {
            return Error("glob", "Search path does not exist.", runtimeProfile.ProjectRoot, approvalState);
        }

        var regex = BuildGlobRegex(pattern);
        var matches = EnumerateWorkspaceFiles(searchRoot)
            .Where(path => regex.IsMatch(Path.GetRelativePath(searchRoot, path).Replace('\\', '/')))
            .Take(100)
            .ToArray();

        return Success("glob", approvalState, searchRoot, string.Join(Environment.NewLine, matches), []);
    }

    private static QwenNativeToolExecutionResult ExecuteGrep(
        QwenRuntimeProfile runtimeProfile,
        JsonElement arguments,
        string approvalState)
    {
        if (!TryGetString(arguments, "pattern", out var pattern))
        {
            return Error("grep_search", "Parameter 'pattern' is required.", runtimeProfile.ProjectRoot, approvalState);
        }

        var searchRoot = TryGetString(arguments, "path", out var pathValue)
            ? ResolvePath(pathValue, runtimeProfile.ProjectRoot)
            : runtimeProfile.ProjectRoot;
        if (!Directory.Exists(searchRoot))
        {
            return Error("grep_search", "Search path does not exist.", runtimeProfile.ProjectRoot, approvalState);
        }

        var limit = Math.Max(1, TryGetInt(arguments, "limit") ?? 100);
        Regex patternRegex;
        try
        {
            patternRegex = new Regex(pattern, RegexOptions.Multiline | RegexOptions.Compiled);
        }
        catch (Exception exception)
        {
            return Error("grep_search", $"Invalid regex: {exception.Message}", runtimeProfile.ProjectRoot, approvalState);
        }

        Regex? globRegex = null;
        if (TryGetString(arguments, "glob", out var glob))
        {
            globRegex = BuildGlobRegex(glob);
        }

        var results = new List<string>();
        foreach (var file in EnumerateWorkspaceFiles(searchRoot))
        {
            var relativePath = Path.GetRelativePath(searchRoot, file).Replace('\\', '/');
            if (globRegex is not null && !globRegex.IsMatch(relativePath))
            {
                continue;
            }

            var lines = File.ReadAllLines(file);
            for (var index = 0; index < lines.Length; index++)
            {
                if (!patternRegex.IsMatch(lines[index]))
                {
                    continue;
                }

                results.Add($"{file}:{index + 1}: {lines[index]}");
                if (results.Count >= limit)
                {
                    return Success("grep_search", approvalState, searchRoot, string.Join(Environment.NewLine, results), []);
                }
            }
        }

        return Success("grep_search", approvalState, searchRoot, string.Join(Environment.NewLine, results), []);
    }

    private static async Task<QwenNativeToolExecutionResult> ExecuteShellAsync(
        QwenRuntimeProfile runtimeProfile,
        JsonElement arguments,
        string approvalState,
        CancellationToken cancellationToken)
    {
        if (!TryGetString(arguments, "command", out var command))
        {
            return Error("run_shell_command", "Parameter 'command' is required.", runtimeProfile.ProjectRoot, approvalState);
        }

        var workingDirectory = TryGetString(arguments, "directory", out var directory)
            ? ResolvePath(directory, runtimeProfile.ProjectRoot)
            : runtimeProfile.ProjectRoot;
        if (!Directory.Exists(workingDirectory))
        {
            return Error("run_shell_command", "Working directory does not exist.", runtimeProfile.ProjectRoot, approvalState);
        }

        var processStartInfo = OperatingSystem.IsWindows()
            ? new ProcessStartInfo("cmd.exe", $"/c {command}")
            : new ProcessStartInfo("/bin/bash", $"-lc \"{command.Replace("\"", "\\\"", StringComparison.Ordinal)}\"");
        processStartInfo.WorkingDirectory = workingDirectory;
        processStartInfo.RedirectStandardOutput = true;
        processStartInfo.RedirectStandardError = true;
        processStartInfo.UseShellExecute = false;
        processStartInfo.CreateNoWindow = true;

        using var process = new Process { StartInfo = processStartInfo };
        process.Start();

        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        return new QwenNativeToolExecutionResult
        {
            ToolName = "run_shell_command",
            Status = process.ExitCode == 0 ? "completed" : "error",
            ApprovalState = approvalState,
            WorkingDirectory = workingDirectory,
            Output = string.IsNullOrWhiteSpace(stderr)
                ? stdout
                : $"{stdout}{Environment.NewLine}{stderr}".Trim(),
            ErrorMessage = process.ExitCode == 0 ? string.Empty : "Shell command exited with a non-zero status.",
            ExitCode = process.ExitCode,
            ChangedFiles = []
        };
    }

    private static async Task<QwenNativeToolExecutionResult> ExecuteWriteFileAsync(
        QwenRuntimeProfile runtimeProfile,
        JsonElement arguments,
        string approvalState,
        CancellationToken cancellationToken)
    {
        var filePath = RequirePath(arguments, "file_path", runtimeProfile.ProjectRoot, absoluteOnly: true);
        if (filePath.IsError)
        {
            return Error("write_file", filePath.ErrorMessage!, runtimeProfile.ProjectRoot, approvalState);
        }

        if (!TryGetString(arguments, "content", out var content))
        {
            return Error("write_file", "Parameter 'content' is required.", runtimeProfile.ProjectRoot, approvalState);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(filePath.Value!)!);
        await File.WriteAllTextAsync(filePath.Value!, content, cancellationToken);

        return Success("write_file", approvalState, runtimeProfile.ProjectRoot, "File written.", [filePath.Value!]);
    }

    private static async Task<QwenNativeToolExecutionResult> ExecuteEditAsync(
        QwenRuntimeProfile runtimeProfile,
        JsonElement arguments,
        string approvalState,
        CancellationToken cancellationToken)
    {
        var filePath = RequirePath(arguments, "file_path", runtimeProfile.ProjectRoot, absoluteOnly: true);
        if (filePath.IsError)
        {
            return Error("edit", filePath.ErrorMessage!, runtimeProfile.ProjectRoot, approvalState);
        }

        if (!TryGetString(arguments, "old_string", out var oldString))
        {
            return Error("edit", "Parameter 'old_string' is required.", runtimeProfile.ProjectRoot, approvalState);
        }

        if (!TryGetString(arguments, "new_string", out var newString))
        {
            return Error("edit", "Parameter 'new_string' is required.", runtimeProfile.ProjectRoot, approvalState);
        }

        var replaceAll = TryGetBool(arguments, "replace_all") ?? false;
        var currentContent = File.Exists(filePath.Value!) ? await File.ReadAllTextAsync(filePath.Value!, cancellationToken) : string.Empty;
        string updatedContent;
        if (string.IsNullOrEmpty(oldString) && !File.Exists(filePath.Value!))
        {
            updatedContent = newString;
        }
        else
        {
            if (!currentContent.Contains(oldString, StringComparison.Ordinal))
            {
                return Error("edit", "Target text was not found in the file.", runtimeProfile.ProjectRoot, approvalState);
            }

            updatedContent = replaceAll
                ? currentContent.Replace(oldString, newString, StringComparison.Ordinal)
                : ReplaceFirst(currentContent, oldString, newString);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(filePath.Value!)!);
        await File.WriteAllTextAsync(filePath.Value!, updatedContent, cancellationToken);

        return Success("edit", approvalState, runtimeProfile.ProjectRoot, "Edit applied.", [filePath.Value!]);
    }

    private static QwenNativeToolExecutionResult Success(
        string toolName,
        string approvalState,
        string workingDirectory,
        string output,
        IReadOnlyList<string> changedFiles) =>
        new()
        {
            ToolName = toolName,
            Status = "completed",
            ApprovalState = approvalState,
            WorkingDirectory = workingDirectory,
            Output = output,
            ChangedFiles = changedFiles
        };

    private static QwenNativeToolExecutionResult Error(
        string toolName,
        string message,
        string workingDirectory,
        string approvalState = "deny") =>
        new()
        {
            ToolName = toolName,
            Status = "error",
            ApprovalState = approvalState,
            WorkingDirectory = workingDirectory,
            ErrorMessage = message,
            ChangedFiles = []
        };

    private static PathResolutionResult RequirePath(
        JsonElement arguments,
        string propertyName,
        string workspaceRoot,
        bool absoluteOnly)
    {
        if (!TryGetString(arguments, propertyName, out var path))
        {
            return PathResolutionResult.Fail($"Parameter '{propertyName}' is required.");
        }

        if (absoluteOnly && !Path.IsPathRooted(path))
        {
            return PathResolutionResult.Fail($"Parameter '{propertyName}' must be an absolute path.");
        }

        var resolved = ResolvePath(path, workspaceRoot);
        if (!IsWithinWorkspace(resolved, workspaceRoot))
        {
            return PathResolutionResult.Fail("Path is outside the workspace root.");
        }

        return PathResolutionResult.Success(resolved);
    }

    private static bool IsWithinWorkspace(string path, string workspaceRoot)
    {
        var fullPath = Path.GetFullPath(path);
        var fullRoot = Path.GetFullPath(workspaceRoot);
        return fullPath.StartsWith(fullRoot, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    }

    private static string ResolvePath(string path, string workspaceRoot) =>
        Path.IsPathRooted(path)
            ? Path.GetFullPath(path)
            : Path.GetFullPath(Path.Combine(workspaceRoot, path));

    private static IEnumerable<string> EnumerateWorkspaceFiles(string root) =>
        Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Where(path => !path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Any(segment => IgnoredDirectories.Contains(segment, StringComparer.OrdinalIgnoreCase)));

    private static bool TryGetString(JsonElement arguments, string propertyName, out string value)
    {
        value = string.Empty;
        if (!arguments.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = property.GetString() ?? string.Empty;
        return !string.IsNullOrWhiteSpace(value);
    }

    private static int? TryGetInt(JsonElement arguments, string propertyName) =>
        arguments.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out var value)
            ? value
            : null;

    private static bool? TryGetBool(JsonElement arguments, string propertyName) =>
        arguments.TryGetProperty(propertyName, out var property) &&
        (property.ValueKind == JsonValueKind.True || property.ValueKind == JsonValueKind.False)
            ? property.GetBoolean()
            : null;

    private static Regex BuildGlobRegex(string pattern)
    {
        var builder = new StringBuilder("^");
        for (var index = 0; index < pattern.Length; index++)
        {
            var character = pattern[index];
            if (character == '*')
            {
                var isDoubleStar = index + 1 < pattern.Length && pattern[index + 1] == '*';
                if (isDoubleStar)
                {
                    builder.Append(".*");
                    index++;
                }
                else
                {
                    builder.Append(@"[^/\\]*");
                }
            }
            else if (character == '?')
            {
                builder.Append('.');
            }
            else
            {
                builder.Append(Regex.Escape(character.ToString()));
            }
        }

        builder.Append('$');
        return new Regex(builder.ToString(), RegexOptions.IgnoreCase | RegexOptions.Compiled);
    }

    private static string ReplaceFirst(string content, string oldString, string newString)
    {
        var index = content.IndexOf(oldString, StringComparison.Ordinal);
        if (index < 0)
        {
            return content;
        }

        return string.Concat(
            content.AsSpan(0, index),
            newString,
            content.AsSpan(index + oldString.Length));
    }

    private sealed record PathResolutionResult(string? Value, string? ErrorMessage)
    {
        public bool IsError => ErrorMessage is not null;

        public static PathResolutionResult Success(string value) => new(value, null);

        public static PathResolutionResult Fail(string errorMessage) => new(null, errorMessage);
    }
}
