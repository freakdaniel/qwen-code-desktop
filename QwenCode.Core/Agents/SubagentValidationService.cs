using QwenCode.App.Models;

namespace QwenCode.App.Agents;

public sealed class SubagentValidationService(
    ISubagentModelSelectionService modelSelectionService) : ISubagentValidationService
{
    public SubagentValidationResult Validate(SubagentDescriptor descriptor)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        ValidateName(descriptor.Name, errors, warnings);

        if (string.IsNullOrWhiteSpace(descriptor.Description))
        {
            errors.Add("Description is required and cannot be empty.");
        }
        else if (descriptor.Description.Length > 1000)
        {
            warnings.Add("Description is very long and may reduce readability.");
        }

        if (string.IsNullOrWhiteSpace(descriptor.SystemPrompt) || descriptor.SystemPrompt.Trim().Length < 10)
        {
            errors.Add("System prompt must contain at least 10 non-whitespace characters.");
        }

        if (descriptor.Tools.Any(static tool => string.IsNullOrWhiteSpace(tool)))
        {
            errors.Add("Tool names cannot be empty.");
        }

        if (!string.IsNullOrWhiteSpace(descriptor.Model))
        {
            try
            {
                _ = modelSelectionService.Parse(descriptor.Model, "openai");
            }
            catch (Exception exception)
            {
                errors.Add(exception.Message);
            }
        }

        if (descriptor.RunConfiguration.MaxTimeMinutes is <= 0)
        {
            errors.Add("max_time_minutes must be greater than 0.");
        }

        if (descriptor.RunConfiguration.MaxTurns is <= 0)
        {
            errors.Add("max_turns must be greater than 0.");
        }

        if (descriptor.RunConfiguration.MaxTurns > 100)
        {
            warnings.Add("Very high max_turns may lead to long-running subagent sessions.");
        }

        return new SubagentValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors,
            Warnings = warnings
        };
    }

    private static void ValidateName(string name, ICollection<string> errors, ICollection<string> warnings)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            errors.Add("Name is required and cannot be empty.");
            return;
        }

        var trimmed = name.Trim();
        if (trimmed.Length < 2 || trimmed.Length > 50)
        {
            errors.Add("Name must be between 2 and 50 characters.");
        }

        if (!trimmed.All(static character => char.IsLetterOrDigit(character) || character is '-' or '_'))
        {
            errors.Add("Name can only contain letters, numbers, hyphens, and underscores.");
        }

        if (trimmed.StartsWith('-') || trimmed.StartsWith('_') || trimmed.EndsWith('-') || trimmed.EndsWith('_'))
        {
            errors.Add("Name cannot start or end with a hyphen or underscore.");
        }

        if (!string.Equals(trimmed, trimmed.ToLowerInvariant(), StringComparison.Ordinal))
        {
            warnings.Add("Consider using lowercase names for consistency.");
        }
    }
}
