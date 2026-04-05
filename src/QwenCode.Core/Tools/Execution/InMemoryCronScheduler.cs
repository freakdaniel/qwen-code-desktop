using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using QwenCode.App.Models;

namespace QwenCode.App.Tools;

/// <summary>
/// Represents the In Memory Cron Scheduler
/// </summary>
public sealed class InMemoryCronScheduler : ICronScheduler
{
    private const int MaxJobs = 50;
    private static readonly TimeSpan RecurringLifetime = TimeSpan.FromDays(3);
    private static readonly TimeSpan MaxRecurringJitter = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan MaxOneShotJitter = TimeSpan.FromSeconds(90);
    private static readonly char[] IdAlphabet = "abcdefghijklmnopqrstuvwxyz0123456789".ToCharArray();

    private readonly ConcurrentDictionary<string, ScheduledCronJob> jobs = new(StringComparer.OrdinalIgnoreCase);
    private readonly object lifecycleSync = new();
    private Timer? timer;
    private Action<ScheduledCronJob>? onFire;

    /// <summary>
    /// Gets the count
    /// </summary>
    public int Count => jobs.Count;

    /// <summary>
    /// Gets a value indicating whether is running
    /// </summary>
    public bool IsRunning => timer is not null;

    /// <summary>
    /// Creates value
    /// </summary>
    /// <param name="cronExpression">The cron expression</param>
    /// <param name="prompt">The prompt content</param>
    /// <param name="recurring">The recurring</param>
    /// <returns>The resulting scheduled cron job</returns>
    public ScheduledCronJob Create(string cronExpression, string prompt, bool recurring)
    {
        CronExpressionParser.Parse(cronExpression);

        if (string.IsNullOrWhiteSpace(prompt))
        {
            throw new InvalidOperationException("Prompt is required.");
        }

        if (jobs.Count >= MaxJobs)
        {
            throw new InvalidOperationException($"Maximum number of cron jobs ({MaxJobs}) reached. Delete some jobs first.");
        }

        var now = DateTimeOffset.Now;
        var id = GenerateId();
        var job = new ScheduledCronJob
        {
            Id = id,
            CronExpression = cronExpression.Trim(),
            Prompt = prompt.Trim(),
            IsRecurring = recurring,
            CreatedAt = now,
            ExpiresAt = recurring ? now.Add(RecurringLifetime) : DateTimeOffset.MaxValue,
            JitterMilliseconds = ComputeJitterMilliseconds(id, cronExpression, recurring, now)
        };

        if (!jobs.TryAdd(id, job))
        {
            throw new InvalidOperationException("Failed to register cron job.");
        }

        return Clone(job);
    }

    /// <summary>
    /// Deletes value
    /// </summary>
    /// <param name="id">The id</param>
    /// <returns>A value indicating whether the operation succeeded</returns>
    public bool Delete(string id) => jobs.TryRemove(id, out _);

    /// <summary>
    /// Lists value
    /// </summary>
    /// <returns>The resulting i read only list scheduled cron job</returns>
    public IReadOnlyList<ScheduledCronJob> List() =>
        jobs.Values
            .OrderBy(static job => job.CreatedAt)
            .ThenBy(static job => job.Id, StringComparer.OrdinalIgnoreCase)
            .Select(Clone)
            .ToArray();

    /// <summary>
    /// Starts value
    /// </summary>
    /// <param name="onFire">The on fire</param>
    public void Start(Action<ScheduledCronJob> onFire)
    {
        ArgumentNullException.ThrowIfNull(onFire);

        lock (lifecycleSync)
        {
            this.onFire = onFire;
            timer ??= new Timer(static state => ((InMemoryCronScheduler)state!).Tick(), this, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
        }
    }

    /// <summary>
    /// Stops value
    /// </summary>
    public void Stop()
    {
        lock (lifecycleSync)
        {
            timer?.Dispose();
            timer = null;
            onFire = null;
        }
    }

    /// <summary>
    /// Executes tick
    /// </summary>
    /// <param name="now">The now</param>
    public void Tick(DateTimeOffset? now = null)
    {
        var currentTime = now ?? DateTimeOffset.Now;
        foreach (var (id, job) in jobs)
        {
            if (currentTime >= job.ExpiresAt)
            {
                jobs.TryRemove(id, out _);
                continue;
            }

            var matchedMinute = TryFindDueCronMinute(job, currentTime);
            if (matchedMinute is null)
            {
                continue;
            }

            if (job.LastFiredAt is { } lastFired && lastFired == matchedMinute.Value)
            {
                continue;
            }

            job.LastFiredAt = matchedMinute.Value;
            if (!job.IsRecurring)
            {
                jobs.TryRemove(id, out _);
            }

            onFire?.Invoke(Clone(job));
        }
    }

    /// <summary>
    /// Gets exit summary
    /// </summary>
    /// <returns>The resulting string?</returns>
    public string? GetExitSummary()
    {
        var activeJobs = List();
        if (activeJobs.Count == 0)
        {
            return null;
        }

        var lines = new List<string>
        {
            $"Session ending. {activeJobs.Count} active loop{(activeJobs.Count == 1 ? string.Empty : "s")} cancelled:"
        };

        foreach (var job in activeJobs)
        {
            var prompt = job.Prompt.Length > 60 ? $"{job.Prompt[..57]}..." : job.Prompt;
            lines.Add($"  - [{job.Id}] {CronHumanizer.ToDisplayString(job.CronExpression)}: {prompt}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    /// <summary>
    /// Executes destroy
    /// </summary>
    public void Destroy()
    {
        Stop();
        jobs.Clear();
    }

    private static ScheduledCronJob Clone(ScheduledCronJob job) => new()
    {
        Id = job.Id,
        CronExpression = job.CronExpression,
        Prompt = job.Prompt,
        IsRecurring = job.IsRecurring,
        CreatedAt = job.CreatedAt,
        ExpiresAt = job.ExpiresAt,
        LastFiredAt = job.LastFiredAt,
        JitterMilliseconds = job.JitterMilliseconds
    };

    private static DateTimeOffset? TryFindDueCronMinute(ScheduledCronJob job, DateTimeOffset now)
    {
        var absJitter = Math.Abs(job.JitterMilliseconds);
        var windowMinutes = (int)Math.Ceiling(absJitter / 60_000d);
        var minuteStart = new DateTimeOffset(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0, now.Offset);

        DateTimeOffset? matchedMinute = null;
        for (var offset = -windowMinutes; offset <= windowMinutes; offset++)
        {
            var candidate = minuteStart.AddMinutes(offset);
            if (!CronExpressionParser.Matches(job.CronExpression, candidate.LocalDateTime))
            {
                continue;
            }

            if (now < candidate.AddMilliseconds(job.JitterMilliseconds))
            {
                continue;
            }

            if (matchedMinute is null || candidate > matchedMinute.Value)
            {
                matchedMinute = candidate;
            }
        }

        return matchedMinute;
    }

    private static int ComputeJitterMilliseconds(
        string id,
        string cronExpression,
        bool recurring,
        DateTimeOffset now)
    {
        var hash = HashId(id);
        if (recurring)
        {
            try
            {
                var first = CronExpressionParser.NextFireTime(cronExpression, now.LocalDateTime);
                var second = CronExpressionParser.NextFireTime(cronExpression, first);
                var period = second - first;
                var maxJitter = Math.Min(period.TotalMilliseconds * 0.1, MaxRecurringJitter.TotalMilliseconds);
                var bucket = Math.Max(1, (int)Math.Floor(maxJitter));
                return hash % bucket;
            }
            catch
            {
                return 0;
            }
        }

        var minuteField = cronExpression.Trim()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault();

        return int.TryParse(minuteField, out var minute) && (minute == 0 || minute == 30)
            ? -(hash % (int)MaxOneShotJitter.TotalMilliseconds)
            : 0;
    }

    private static int HashId(string id)
    {
        var hash = 0;
        foreach (var character in id)
        {
            hash = ((hash * 31) + character) & int.MaxValue;
        }

        return Math.Abs(hash);
    }

    private static string GenerateId()
    {
        Span<byte> buffer = stackalloc byte[8];
        RandomNumberGenerator.Fill(buffer);
        var builder = new StringBuilder(8);
        foreach (var value in buffer)
        {
            builder.Append(IdAlphabet[value % IdAlphabet.Length]);
        }

        return builder.ToString();
    }

    private sealed class CronExpressionParser
    {
        private static readonly (int Min, int Max)[] FieldRanges =
        [
            (0, 59),
            (0, 23),
            (1, 31),
            (1, 12),
            (0, 7)
        ];

        /// <summary>
        /// Executes parse
        /// </summary>
        /// <param name="cronExpression">The cron expression</param>
        /// <returns>The resulting cron fields</returns>
        public static CronFields Parse(string cronExpression)
        {
            var parts = cronExpression.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length != 5)
            {
                throw new InvalidOperationException($"Cron expression must have exactly 5 fields, got {parts.Length}: \"{cronExpression}\"");
            }

            var dayOfWeek = ParseField(parts[4], FieldRanges[4].Min, FieldRanges[4].Max);
            if (dayOfWeek.Remove(7))
            {
                dayOfWeek.Add(0);
            }

            return new CronFields(
                Minute: ParseField(parts[0], FieldRanges[0].Min, FieldRanges[0].Max),
                Hour: ParseField(parts[1], FieldRanges[1].Min, FieldRanges[1].Max),
                DayOfMonth: ParseField(parts[2], FieldRanges[2].Min, FieldRanges[2].Max),
                Month: ParseField(parts[3], FieldRanges[3].Min, FieldRanges[3].Max),
                DayOfWeek: dayOfWeek,
                DayOfMonthIsWildcard: parts[2] == "*",
                DayOfWeekIsWildcard: parts[4] == "*");
        }

        /// <summary>
        /// Executes matches
        /// </summary>
        /// <param name="cronExpression">The cron expression</param>
        /// <param name="date">The date</param>
        /// <returns>A value indicating whether the operation succeeded</returns>
        public static bool Matches(string cronExpression, DateTime date)
        {
            var fields = Parse(cronExpression);
            if (!fields.Minute.Contains(date.Minute) ||
                !fields.Hour.Contains(date.Hour) ||
                !fields.Month.Contains(date.Month))
            {
                return false;
            }

            var dayOfMonthMatches = fields.DayOfMonth.Contains(date.Day);
            var dayOfWeekMatches = fields.DayOfWeek.Contains((int)date.DayOfWeek);

            return !fields.DayOfMonthIsWildcard && !fields.DayOfWeekIsWildcard
                ? dayOfMonthMatches || dayOfWeekMatches
                : dayOfMonthMatches && dayOfWeekMatches;
        }

        /// <summary>
        /// Executes next fire time
        /// </summary>
        /// <param name="cronExpression">The cron expression</param>
        /// <param name="after">The after</param>
        /// <returns>The resulting date time</returns>
        public static DateTime NextFireTime(string cronExpression, DateTime after)
        {
            var fields = Parse(cronExpression);
            var candidate = new DateTime(after.Year, after.Month, after.Day, after.Hour, after.Minute, 0, after.Kind).AddMinutes(1);

            const int maxIterations = 4 * 366 * 24 * 60;
            for (var index = 0; index < maxIterations; index++)
            {
                var minuteMatches = fields.Minute.Contains(candidate.Minute);
                var hourMatches = fields.Hour.Contains(candidate.Hour);
                var monthMatches = fields.Month.Contains(candidate.Month);
                var dayOfMonthMatches = fields.DayOfMonth.Contains(candidate.Day);
                var dayOfWeekMatches = fields.DayOfWeek.Contains((int)candidate.DayOfWeek);
                var dayMatches = !fields.DayOfMonthIsWildcard && !fields.DayOfWeekIsWildcard
                    ? dayOfMonthMatches || dayOfWeekMatches
                    : dayOfMonthMatches && dayOfWeekMatches;

                if (minuteMatches && hourMatches && monthMatches && dayMatches)
                {
                    return candidate;
                }

                candidate = candidate.AddMinutes(1);
            }

            throw new InvalidOperationException($"No matching fire time found within 4 years for: \"{cronExpression}\"");
        }

        private static HashSet<int> ParseField(string field, int min, int max)
        {
            var values = new HashSet<int>();
            foreach (var segment in field.Split(',', StringSplitOptions.TrimEntries))
            {
                if (string.IsNullOrWhiteSpace(segment))
                {
                    throw new InvalidOperationException($"Empty field segment in \"{field}\"");
                }

                var stepParts = segment.Split('/', StringSplitOptions.TrimEntries);
                if (stepParts.Length > 2)
                {
                    throw new InvalidOperationException($"Invalid step expression: \"{segment}\"");
                }

                var baseExpression = stepParts[0];
                var step = stepParts.Length == 2 && int.TryParse(stepParts[1], out var parsedStep) ? parsedStep : 1;
                if (step <= 0)
                {
                    throw new InvalidOperationException($"Invalid step: \"{(stepParts.Length == 2 ? stepParts[1] : string.Empty)}\"");
                }

                var (start, end) = baseExpression switch
                {
                    "*" => (min, max),
                    _ when baseExpression.Contains('-', StringComparison.Ordinal) => ParseRange(baseExpression, min, max),
                    _ => ParseSingle(baseExpression, min, max)
                };

                for (var value = start; value <= end; value += step)
                {
                    values.Add(value);
                }
            }

            return values;
        }

        private static (int Start, int End) ParseRange(string expression, int min, int max)
        {
            var parts = expression.Split('-', StringSplitOptions.TrimEntries);
            if (parts.Length != 2 ||
                !int.TryParse(parts[0], out var start) ||
                !int.TryParse(parts[1], out var end) ||
                start < min ||
                end > max ||
                start > end)
            {
                throw new InvalidOperationException($"Range {expression} out of bounds [{min}-{max}]");
            }

            return (start, end);
        }

        private static (int Start, int End) ParseSingle(string expression, int min, int max)
        {
            if (!int.TryParse(expression, out var value) || value < min || value > max)
            {
                throw new InvalidOperationException($"Value \"{expression}\" out of bounds [{min}-{max}]");
            }

            return (value, value);
        }
    }

    private sealed class CronHumanizer
    {
        /// <summary>
        /// Executes to display string
        /// </summary>
        /// <param name="cronExpression">The cron expression</param>
        /// <returns>The resulting string</returns>
        public static string ToDisplayString(string cronExpression)
        {
            var parts = cronExpression.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length != 5)
            {
                return cronExpression;
            }

            var minute = parts[0];
            var hour = parts[1];
            var dayOfMonth = parts[2];
            var month = parts[3];
            var dayOfWeek = parts[4];

            if (minute.StartsWith("*/", StringComparison.Ordinal) &&
                hour == "*" &&
                dayOfMonth == "*" &&
                month == "*" &&
                dayOfWeek == "*" &&
                int.TryParse(minute[2..], out var minuteStep))
            {
                return minuteStep == 1 ? "Every minute" : $"Every {minuteStep} minutes";
            }

            if (int.TryParse(minute, out _) &&
                hour.StartsWith("*/", StringComparison.Ordinal) &&
                dayOfMonth == "*" &&
                month == "*" &&
                dayOfWeek == "*" &&
                int.TryParse(hour[2..], out var hourStep))
            {
                return hourStep == 1 ? "Every hour" : $"Every {hourStep} hours";
            }

            if (int.TryParse(minute, out _) &&
                int.TryParse(hour, out _) &&
                dayOfMonth.StartsWith("*/", StringComparison.Ordinal) &&
                month == "*" &&
                dayOfWeek == "*" &&
                int.TryParse(dayOfMonth[2..], out var dayStep))
            {
                return dayStep == 1 ? "Every day" : $"Every {dayStep} days";
            }

            return cronExpression;
        }
    }

    private sealed record CronFields(
        HashSet<int> Minute,
        HashSet<int> Hour,
        HashSet<int> DayOfMonth,
        HashSet<int> Month,
        HashSet<int> DayOfWeek,
        bool DayOfMonthIsWildcard,
        bool DayOfWeekIsWildcard);
}
