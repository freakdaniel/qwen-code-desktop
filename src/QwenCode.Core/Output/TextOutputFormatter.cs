using QwenCode.Core.Models;

namespace QwenCode.Core.Output;

/// <summary>
/// Represents the Text Output Formatter
/// </summary>
public sealed class TextOutputFormatter : IOutputFormatter
{
    /// <summary>
    /// Executes format
    /// </summary>
    /// <typeparam name="T">The type of t</typeparam>
    /// <param name="value">The value</param>
    /// <param name="format">The format</param>
    /// <returns>The resulting string</returns>
    public string Format<T>(T value, OutputFormat format)
    {
        if (format != OutputFormat.Text)
        {
            throw new InvalidOperationException($"Formatter '{nameof(TextOutputFormatter)}' does not support '{format}'.");
        }

        return value switch
        {
            SessionExportSnapshot sessionSnapshot => FormatSession(sessionSnapshot),
            null => string.Empty,
            _ => value?.ToString() ?? string.Empty
        };
    }

    private static string FormatSession(SessionExportSnapshot snapshot)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Session: {ResolveSessionTitle(snapshot)}");
        builder.AppendLine($"SessionId: {snapshot.Session.SessionId}");
        builder.AppendLine($"Status: {snapshot.Session.Status}");
        builder.AppendLine($"WorkingDirectory: {snapshot.Session.WorkingDirectory}");
        builder.AppendLine($"TranscriptPath: {snapshot.TranscriptPath}");
        builder.AppendLine($"EntryCount: {snapshot.EntryCount}");
        builder.AppendLine($"ExportedAtUtc: {snapshot.ExportedAtUtc:O}");
        builder.AppendLine();
        builder.AppendLine("Summary:");
        builder.AppendLine($"  UserMessages: {snapshot.Summary.UserCount}");
        builder.AppendLine($"  AssistantMessages: {snapshot.Summary.AssistantCount}");
        builder.AppendLine($"  ToolEntries: {snapshot.Summary.ToolCount}");
        builder.AppendLine($"  CommandEntries: {snapshot.Summary.CommandCount}");
        builder.AppendLine($"  PendingApprovals: {snapshot.Summary.PendingApprovalCount}");
        builder.AppendLine($"  PendingQuestions: {snapshot.Summary.PendingQuestionCount}");
        builder.AppendLine($"  CompletedTools: {snapshot.Summary.CompletedToolCount}");
        builder.AppendLine($"  FailedTools: {snapshot.Summary.FailedToolCount}");
        if (!string.IsNullOrWhiteSpace(snapshot.Summary.LastTimestamp))
        {
            builder.AppendLine($"  LastTimestamp: {snapshot.Summary.LastTimestamp}");
        }

        if (snapshot.Entries.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("Entries:");
            foreach (var entry in snapshot.Entries)
            {
                builder.Append("- ");
                builder.Append(entry.Timestamp);
                builder.Append(' ');
                builder.Append(entry.Type);
                if (!string.IsNullOrWhiteSpace(entry.ToolName))
                {
                    builder.Append(" [");
                    builder.Append(entry.ToolName);
                    builder.Append(']');
                }

                if (!string.IsNullOrWhiteSpace(entry.Status))
                {
                    builder.Append(" {");
                    builder.Append(entry.Status);
                    builder.Append('}');
                }

                if (!string.IsNullOrWhiteSpace(entry.Body))
                {
                    builder.Append(": ");
                    builder.Append(entry.Body);
                }

                builder.AppendLine();
            }
        }

        return builder.ToString().TrimEnd();
    }

    private static string ResolveSessionTitle(SessionExportSnapshot snapshot)
    {
        if (!string.IsNullOrWhiteSpace(snapshot.Session.Title))
        {
            return snapshot.Session.Title;
        }

        var firstUserPrompt = snapshot.Entries
            .FirstOrDefault(static entry => string.Equals(entry.Type, "user", StringComparison.OrdinalIgnoreCase) &&
                                            !string.IsNullOrWhiteSpace(entry.Body))
            ?.Body;
        if (!string.IsNullOrWhiteSpace(firstUserPrompt))
        {
            return firstUserPrompt;
        }

        return snapshot.Session.SessionId;
    }
}
