using System.Diagnostics;
using System.Text.Json;
using QwenCode.App.Models;

namespace QwenCode.App.Hooks;

public sealed class HookCommandRunner
{
    private const int BlockingExitCode = 2;
    private const int NonBlockingExitCode = 1;
    private const int MaximumOutputLength = 1_048_576;

    public async Task<HookExecutionResult> ExecuteAsync(
        CommandHookConfiguration hook,
        UserPromptHookRequest request,
        CancellationToken cancellationToken = default)
    {
        var startTimestamp = Stopwatch.GetTimestamp();
        var payload = JsonSerializer.Serialize(new
        {
            session_id = request.SessionId,
            transcript_path = request.TranscriptPath,
            cwd = request.WorkingDirectory,
            hook_event_name = HookEventName.UserPromptSubmit.ToString(),
            timestamp = DateTimeOffset.UtcNow.ToString("O"),
            prompt = request.Prompt
        });

        using var process = new Process
        {
            StartInfo = CreateStartInfo(hook, request.WorkingDirectory)
        };

        foreach (var item in hook.EnvironmentVariables)
        {
            process.StartInfo.Environment[item.Key] = item.Value;
        }

        try
        {
            process.Start();
        }
        catch (Exception exception)
        {
            return new HookExecutionResult
            {
                Hook = hook,
                Success = false,
                ErrorMessage = exception.Message,
                Duration = Stopwatch.GetElapsedTime(startTimestamp)
            };
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(hook.TimeoutMs);
        var token = timeoutCts.Token;

        try
        {
            await process.StandardInput.WriteAsync(payload.AsMemory(), token);
            await process.StandardInput.FlushAsync();
            process.StandardInput.Close();

            var stdoutTask = process.StandardOutput.ReadToEndAsync(token);
            var stderrTask = process.StandardError.ReadToEndAsync(token);
            await process.WaitForExitAsync(token);

            var stdout = TrimOutput(await stdoutTask);
            var stderr = TrimOutput(await stderrTask);
            var exitCode = process.ExitCode;

            return new HookExecutionResult
            {
                Hook = hook,
                Success = exitCode == 0,
                ExitCode = exitCode,
                StandardOutput = stdout,
                StandardError = stderr,
                Output = ParseOutput(stdout, stderr, exitCode),
                Duration = Stopwatch.GetElapsedTime(startTimestamp)
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            TryTerminate(process);
            return new HookExecutionResult
            {
                Hook = hook,
                Success = false,
                ExitCode = -1,
                ErrorMessage = $"Hook timed out after {hook.TimeoutMs} ms.",
                Duration = Stopwatch.GetElapsedTime(startTimestamp)
            };
        }
        catch (OperationCanceledException)
        {
            TryTerminate(process);
            return new HookExecutionResult
            {
                Hook = hook,
                Success = false,
                ExitCode = -1,
                ErrorMessage = "Hook execution was cancelled.",
                Duration = Stopwatch.GetElapsedTime(startTimestamp)
            };
        }
        catch (Exception exception)
        {
            TryTerminate(process);
            return new HookExecutionResult
            {
                Hook = hook,
                Success = false,
                ExitCode = -1,
                ErrorMessage = exception.Message,
                Duration = Stopwatch.GetElapsedTime(startTimestamp)
            };
        }
    }

    private static ProcessStartInfo CreateStartInfo(CommandHookConfiguration hook, string workingDirectory)
    {
        var command = ExpandCommand(hook.Command, workingDirectory);
        if (OperatingSystem.IsWindows())
        {
            var wrappedCommand = $"& {{ {command}; exit $LASTEXITCODE }}";
            return new ProcessStartInfo("powershell.exe", $"-NoProfile -NonInteractive -Command \"{wrappedCommand.Replace("\"", "\\\"", StringComparison.Ordinal)}\"")
            {
                WorkingDirectory = workingDirectory,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
        }

        return new ProcessStartInfo("/bin/sh", $"-lc \"{command.Replace("\"", "\\\"", StringComparison.Ordinal)}\"")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
    }

    private static string ExpandCommand(string command, string workingDirectory)
    {
        var escapedWorkingDirectory = OperatingSystem.IsWindows()
            ? $"'{workingDirectory.Replace("'", "''", StringComparison.Ordinal)}'"
            : $"'{workingDirectory.Replace("'", "'\\''", StringComparison.Ordinal)}'";
        return command
            .Replace("$QWEN_PROJECT_DIR", escapedWorkingDirectory, StringComparison.Ordinal)
            .Replace("$GEMINI_PROJECT_DIR", escapedWorkingDirectory, StringComparison.Ordinal)
            .Replace("$CLAUDE_PROJECT_DIR", escapedWorkingDirectory, StringComparison.Ordinal);
    }

    private static HookOutput ParseOutput(string stdout, string stderr, int exitCode)
    {
        var text = exitCode == BlockingExitCode
            ? stderr.Trim()
            : string.IsNullOrWhiteSpace(stdout) ? stderr.Trim() : stdout.Trim();

        if (string.IsNullOrWhiteSpace(text))
        {
            return exitCode switch
            {
                0 => new HookOutput { Decision = "allow" },
                NonBlockingExitCode => new HookOutput
                {
                    Decision = "allow",
                    Reason = "Hook returned a non-blocking warning."
                },
                _ => new HookOutput
                {
                    Decision = "deny",
                    Reason = "Hook blocked execution."
                }
            };
        }

        if (exitCode != BlockingExitCode && TryParseJsonOutput(text, out var output))
        {
            return output;
        }

        return exitCode switch
        {
            0 => new HookOutput
            {
                Decision = "allow",
                SystemMessage = text,
                Reason = "Hook executed successfully."
            },
            NonBlockingExitCode => new HookOutput
            {
                Decision = "allow",
                SystemMessage = text,
                Reason = $"Non-blocking hook warning: {text}"
            },
            _ => new HookOutput
            {
                Decision = "deny",
                Reason = text
            }
        };
    }

    private static bool TryParseJsonOutput(string text, out HookOutput output)
    {
        output = null!;
        try
        {
            using var document = JsonDocument.Parse(text);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            output = new HookOutput
            {
                Continue = TryGetBoolean(root, "continue"),
                StopReason = TryGetString(root, "stopReason"),
                Decision = TryGetString(root, "decision"),
                Reason = TryGetString(root, "reason"),
                SystemMessage = TryGetString(root, "systemMessage"),
                AdditionalContext = TryGetNestedString(root, "hookSpecificOutput", "additionalContext"),
                ModifiedPrompt = TryGetNestedString(root, "hookSpecificOutput", "modifiedPrompt")
            };
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string TrimOutput(string value) =>
        value.Length <= MaximumOutputLength ? value : value[..MaximumOutputLength];

    private static void TryTerminate(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(true);
            }
        }
        catch
        {
            // Ignore cleanup failures when terminating hook processes.
        }
    }

    private static bool? TryGetBoolean(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var value) ||
            value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            return null;
        }

        return value.GetBoolean();
    }

    private static string TryGetString(JsonElement root, string propertyName) =>
        root.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    private static string TryGetNestedString(JsonElement root, string parentPropertyName, string childPropertyName)
    {
        if (!root.TryGetProperty(parentPropertyName, out var parent) || parent.ValueKind != JsonValueKind.Object)
        {
            return string.Empty;
        }

        return parent.TryGetProperty(childPropertyName, out var child) && child.ValueKind == JsonValueKind.String
            ? child.GetString() ?? string.Empty
            : string.Empty;
    }
}
