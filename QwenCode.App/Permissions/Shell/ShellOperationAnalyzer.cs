using System.Text;
using QwenCode.App.Models;

namespace QwenCode.App.Permissions;

internal static class ShellOperationAnalyzer
{
    private static readonly HashSet<string> PrefixCommands =
    [
        "sudo",
        "doas",
        "env",
        "time",
        "nice",
        "ionice",
        "nohup",
        "timeout",
        "unbuffer",
        "stdbuf"
    ];

    private static readonly HashSet<string> ReadCommands =
    [
        "cat",
        "tac",
        "nl",
        "head",
        "tail",
        "less",
        "more",
        "type"
    ];

    private static readonly HashSet<string> ListCommands =
    [
        "ls",
        "dir",
        "tree",
        "find"
    ];

    private static readonly HashSet<string> SearchCommands =
    [
        "grep",
        "rg",
        "findstr",
        "ag"
    ];

    private static readonly HashSet<string> WriteCommands =
    [
        "touch",
        "mkdir",
        "mkfifo",
        "tee"
    ];

    private static readonly HashSet<string> EditCommands =
    [
        "rm",
        "rmdir",
        "unlink",
        "shred",
        "truncate",
        "chmod",
        "chown",
        "chgrp",
        "rename"
    ];

    private static readonly HashSet<string> WebCommands =
    [
        "curl",
        "wget",
        "fetch"
    ];

    public static IReadOnlyList<ShellOperation> ExtractOperations(string? command, string workingDirectory)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return [];
        }

        var operations = new List<ShellOperation>();
        foreach (var simpleCommand in SplitCompoundCommands(command))
        {
            operations.AddRange(ExtractFromSimpleCommand(simpleCommand, workingDirectory));
        }

        return operations;
    }

    private static IReadOnlyList<ShellOperation> ExtractFromSimpleCommand(string simpleCommand, string workingDirectory)
    {
        if (string.IsNullOrWhiteSpace(simpleCommand))
        {
            return [];
        }

        var tokens = Tokenize(simpleCommand);
        if (tokens.Count == 0)
        {
            return [];
        }

        var redirectOperations = ExtractRedirectOperations(tokens, workingDirectory);
        var commandIndex = FindActualCommandIndex(tokens);
        if (commandIndex < 0 || commandIndex >= tokens.Count)
        {
            return redirectOperations;
        }

        var commandName = tokens[commandIndex];
        if (string.IsNullOrWhiteSpace(commandName) || commandName.Contains('=', StringComparison.Ordinal))
        {
            return redirectOperations;
        }

        if (PrefixCommands.Contains(commandName))
        {
            var innerCommand = string.Join(' ', tokens.Skip(commandIndex + 1));
            return [.. ExtractOperations(innerCommand, workingDirectory), .. redirectOperations];
        }

        var args = tokens.Skip(commandIndex + 1).ToArray();
        var operations = new List<ShellOperation>();

        if (ReadCommands.Contains(commandName))
        {
            operations.AddRange(ReadFileOps(args, workingDirectory));
        }
        else if (ListCommands.Contains(commandName))
        {
            operations.AddRange(ListDirectoryOps(args, workingDirectory));
        }
        else if (SearchCommands.Contains(commandName))
        {
            operations.AddRange(SearchOps(args, workingDirectory));
        }
        else if (WriteCommands.Contains(commandName))
        {
            operations.AddRange(WriteOps(args, workingDirectory));
        }
        else if (EditCommands.Contains(commandName))
        {
            operations.AddRange(EditOps(args, workingDirectory));
        }
        else if (string.Equals(commandName, "cp", StringComparison.Ordinal))
        {
            operations.AddRange(CopyOps(args, workingDirectory));
        }
        else if (string.Equals(commandName, "mv", StringComparison.Ordinal))
        {
            operations.AddRange(MoveOps(args, workingDirectory));
        }
        else if (string.Equals(commandName, "sed", StringComparison.Ordinal))
        {
            operations.AddRange(SedOps(args, workingDirectory));
        }
        else if (WebCommands.Contains(commandName))
        {
            operations.AddRange(WebOps(args));
        }

        operations.AddRange(redirectOperations);
        return operations;
    }

    private static IReadOnlyList<string> SplitCompoundCommands(string command)
    {
        var commands = new List<string>();
        var builder = new StringBuilder();
        var inSingleQuote = false;
        var inDoubleQuote = false;
        var escaped = false;

        for (var index = 0; index < command.Length; index++)
        {
            var character = command[index];

            if (escaped)
            {
                builder.Append(character);
                escaped = false;
                continue;
            }

            if (character == '\\')
            {
                var nextCharacter = index + 1 < command.Length ? command[index + 1] : '\0';
                if (nextCharacter is '\\' or '"' or '\'' or ' ' or '\t')
                {
                    builder.Append(character);
                    escaped = true;
                }
                else
                {
                    builder.Append(character);
                }
                continue;
            }

            if (character == '\'' && !inDoubleQuote)
            {
                inSingleQuote = !inSingleQuote;
                builder.Append(character);
                continue;
            }

            if (character == '"' && !inSingleQuote)
            {
                inDoubleQuote = !inDoubleQuote;
                builder.Append(character);
                continue;
            }

            if (!inSingleQuote && !inDoubleQuote && IsCompoundOperator(command, index, out var operatorLength))
            {
                var segment = builder.ToString().Trim();
                if (segment.Length > 0)
                {
                    commands.Add(segment);
                }

                builder.Clear();
                index += operatorLength - 1;
                continue;
            }

            builder.Append(character);
        }

        var tail = builder.ToString().Trim();
        if (tail.Length > 0)
        {
            commands.Add(tail);
        }

        return commands.Count > 0 ? commands : [command.Trim()];
    }

    private static bool IsCompoundOperator(string command, int index, out int operatorLength)
    {
        foreach (var @operator in new[] { "&&", "||", ";;", "|&", "|", ";" })
        {
            if (index + @operator.Length <= command.Length &&
                command.AsSpan(index, @operator.Length).Equals(@operator.AsSpan(), StringComparison.Ordinal))
            {
                operatorLength = @operator.Length;
                return true;
            }
        }

        operatorLength = 0;
        return false;
    }

    private static List<string> Tokenize(string command)
    {
        var tokens = new List<string>();
        var current = new StringBuilder();
        var inSingleQuote = false;
        var inDoubleQuote = false;
        var escaped = false;

        for (var index = 0; index < command.Length; index++)
        {
            var character = command[index];
            if (escaped)
            {
                current.Append(character);
                escaped = false;
                continue;
            }

            if (character == '\\' && !inSingleQuote)
            {
                var nextCharacter = index + 1 < command.Length ? command[index + 1] : '\0';
                if (nextCharacter is '\\' or '"' or '\'' or ' ' or '\t')
                {
                    escaped = true;
                }
                else
                {
                    current.Append(character);
                }
                continue;
            }

            if (character == '\'' && !inDoubleQuote)
            {
                inSingleQuote = !inSingleQuote;
                continue;
            }

            if (character == '"' && !inSingleQuote)
            {
                inDoubleQuote = !inDoubleQuote;
                continue;
            }

            if (!inSingleQuote && !inDoubleQuote && char.IsWhiteSpace(character))
            {
                if (current.Length > 0)
                {
                    tokens.Add(current.ToString());
                    current.Clear();
                }

                continue;
            }

            current.Append(character);
        }

        if (current.Length > 0)
        {
            tokens.Add(current.ToString());
        }

        return tokens;
    }

    private static int FindActualCommandIndex(IReadOnlyList<string> tokens)
    {
        for (var index = 0; index < tokens.Count; index++)
        {
            var token = tokens[index];
            if (string.IsNullOrWhiteSpace(token))
            {
                continue;
            }

            if (token.StartsWith("-", StringComparison.Ordinal))
            {
                continue;
            }

            if (token.Contains('=', StringComparison.Ordinal))
            {
                continue;
            }

            if (string.Equals(token, "timeout", StringComparison.Ordinal) &&
                index + 1 < tokens.Count &&
                double.TryParse(tokens[index + 1], out _))
            {
                return index;
            }

            return index;
        }

        return -1;
    }

    private static IReadOnlyList<ShellOperation> ExtractRedirectOperations(List<string> tokens, string workingDirectory)
    {
        var operations = new List<ShellOperation>();
        var filtered = new List<string>();

        for (var index = 0; index < tokens.Count; index++)
        {
            var token = tokens[index];
            if (IsStandaloneRedirect(token))
            {
                if (index + 1 < tokens.Count && LooksLikePath(tokens[index + 1]))
                {
                    var redirectTarget = ResolvePath(tokens[index + 1], workingDirectory);
                    operations.Add(new ShellOperation
                    {
                        VirtualTool = token == "<" ? "read_file" : "write_file",
                        FilePath = redirectTarget
                    });

                    index++;
                    continue;
                }
            }

            var combined = ParseCombinedRedirect(token);
            if (combined is not null && LooksLikePath(combined.Value.Target))
            {
                operations.Add(new ShellOperation
                {
                    VirtualTool = combined.Value.Operator == "<" ? "read_file" : "write_file",
                    FilePath = ResolvePath(combined.Value.Target, workingDirectory)
                });
                continue;
            }

            filtered.Add(token);
        }

        tokens.Clear();
        tokens.AddRange(filtered);
        return operations;
    }

    private static bool IsStandaloneRedirect(string token) =>
        token is ">" or "1>" or ">>" or "1>>" or "2>" or "2>>" or "&>" or "&>>" or "<";

    private static (string Operator, string Target)? ParseCombinedRedirect(string token)
    {
        foreach (var @operator in new[] { ">>", ">", "2>>", "2>", "&>>", "&>", "<" })
        {
            if (token.StartsWith(@operator, StringComparison.Ordinal) && token.Length > @operator.Length)
            {
                return (@operator, token[@operator.Length..]);
            }
        }

        return null;
    }

    private static IEnumerable<ShellOperation> ReadFileOps(string[] args, string workingDirectory) =>
        GetPositionalArgs(args)
            .Where(LooksLikePath)
            .Select(path => new ShellOperation
            {
                VirtualTool = "read_file",
                FilePath = ResolvePath(path, workingDirectory)
            });

    private static IEnumerable<ShellOperation> ListDirectoryOps(string[] args, string workingDirectory)
    {
        var directories = GetPositionalArgs(args).Where(LooksLikePath).ToArray();
        if (directories.Length == 0)
        {
            return [new ShellOperation { VirtualTool = "list_directory", FilePath = ResolvePath(".", workingDirectory) }];
        }

        return directories.Select(path => new ShellOperation
        {
            VirtualTool = "list_directory",
            FilePath = ResolvePath(path, workingDirectory)
        });
    }

    private static IEnumerable<ShellOperation> SearchOps(string[] args, string workingDirectory)
    {
        var positional = GetPositionalArgs(args).Where(LooksLikePath).ToArray();
        var searchTarget = positional.Length > 0 ? positional[^1] : ".";

        return
        [
            new ShellOperation
            {
                VirtualTool = "grep_search",
                FilePath = ResolvePath(searchTarget, workingDirectory)
            }
        ];
    }

    private static IEnumerable<ShellOperation> WriteOps(string[] args, string workingDirectory) =>
        GetPositionalArgs(args).Where(LooksLikePath).Select(path => new ShellOperation
        {
            VirtualTool = "write_file",
            FilePath = ResolvePath(path, workingDirectory)
        });

    private static IEnumerable<ShellOperation> EditOps(string[] args, string workingDirectory) =>
        GetPositionalArgs(args).Where(LooksLikePath).Select(path => new ShellOperation
        {
            VirtualTool = "edit",
            FilePath = ResolvePath(path, workingDirectory)
        });

    private static IEnumerable<ShellOperation> CopyOps(string[] args, string workingDirectory)
    {
        var positional = GetPositionalArgs(args).Where(LooksLikePath).ToArray();
        if (positional.Length == 0)
        {
            return [];
        }

        if (positional.Length == 1)
        {
            return
            [
                new ShellOperation
                {
                    VirtualTool = "read_file",
                    FilePath = ResolvePath(positional[0], workingDirectory)
                }
            ];
        }

        var sourceOperations = positional[..^1].Select(path => new ShellOperation
        {
            VirtualTool = "read_file",
            FilePath = ResolvePath(path, workingDirectory)
        });
        var destinationOperation = new ShellOperation
        {
            VirtualTool = "write_file",
            FilePath = ResolvePath(positional[^1], workingDirectory)
        };

        return [.. sourceOperations, destinationOperation];
    }

    private static IEnumerable<ShellOperation> MoveOps(string[] args, string workingDirectory)
    {
        var positional = GetPositionalArgs(args).Where(LooksLikePath).ToArray();
        if (positional.Length < 2)
        {
            return [];
        }

        var sourceOperations = positional[..^1].Select(path => new ShellOperation
        {
            VirtualTool = "edit",
            FilePath = ResolvePath(path, workingDirectory)
        });
        var destinationOperation = new ShellOperation
        {
            VirtualTool = "write_file",
            FilePath = ResolvePath(positional[^1], workingDirectory)
        };

        return [.. sourceOperations, destinationOperation];
    }

    private static IEnumerable<ShellOperation> SedOps(string[] args, string workingDirectory)
    {
        var inPlace = args.Any(arg => arg == "-i" || arg.StartsWith("-i", StringComparison.Ordinal));
        var tool = inPlace ? "edit" : "read_file";
        var positional = GetPositionalArgs(args).Where(LooksLikePath).ToArray();
        var fileArguments = positional.Length > 0 ? positional[1..] : [];

        return fileArguments.Select(path => new ShellOperation
        {
            VirtualTool = tool,
            FilePath = ResolvePath(path, workingDirectory)
        });
    }

    private static IEnumerable<ShellOperation> WebOps(string[] args)
    {
        foreach (var token in GetPositionalArgs(args))
        {
            if (!token.Contains("://", StringComparison.Ordinal) &&
                !token.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !token.StartsWith("https://", StringComparison.OrdinalIgnoreCase) &&
                !token.StartsWith("ftp://", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (Uri.TryCreate(token, UriKind.Absolute, out var uri) && !string.IsNullOrWhiteSpace(uri.Host))
            {
                yield return new ShellOperation
                {
                    VirtualTool = "web_fetch",
                    Domain = uri.Host
                };
            }
        }
    }

    private static IEnumerable<string> GetPositionalArgs(string[] args)
    {
        var positional = new List<string>();
        var skipNext = false;

        foreach (var argument in args)
        {
            if (skipNext)
            {
                skipNext = false;
                continue;
            }

            if (!argument.StartsWith("-", StringComparison.Ordinal))
            {
                positional.Add(argument);
                continue;
            }

            if (FlagConsumesValue(argument))
            {
                skipNext = true;
            }
        }

        return positional;
    }

    private static bool FlagConsumesValue(string argument) =>
        argument is "-n" or "-c" or "--lines" or "--bytes" or "-p" or "--path" or "-e" or "-f" or "-o" or "--output" or "-d" or "--data" or "-H" or "--header" or "-X" or "--request" or "-F" or "--form";

    private static bool LooksLikePath(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (value.StartsWith('$'))
        {
            return false;
        }

        if (value.StartsWith('-'))
        {
            return false;
        }

        if (value.Contains("://", StringComparison.Ordinal))
        {
            return false;
        }

        if (value.Contains('{', StringComparison.Ordinal) || value.Contains('}', StringComparison.Ordinal))
        {
            return false;
        }

        return !value.All(char.IsDigit);
    }

    private static string ResolvePath(string path, string workingDirectory)
    {
        var normalizedPath = path.Replace('\\', '/');
        if (normalizedPath == "~" || normalizedPath.StartsWith("~/", StringComparison.Ordinal))
        {
            var homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile).Replace('\\', '/');
            return normalizedPath.Length == 1
                ? homeDirectory
                : Path.GetFullPath(Path.Combine(homeDirectory, normalizedPath[2..])).Replace('\\', '/');
        }

        if (Path.IsPathRooted(path))
        {
            return Path.GetFullPath(path).Replace('\\', '/');
        }

        return Path.GetFullPath(Path.Combine(workingDirectory, path)).Replace('\\', '/');
    }
}
