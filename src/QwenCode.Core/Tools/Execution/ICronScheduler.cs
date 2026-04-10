using QwenCode.Core.Models;

namespace QwenCode.Core.Tools;

/// <summary>
/// Defines the contract for Cron Scheduler
/// </summary>
public interface ICronScheduler
{
    /// <summary>
    /// Creates value
    /// </summary>
    /// <param name="cronExpression">The cron expression</param>
    /// <param name="prompt">The prompt content</param>
    /// <param name="recurring">The recurring</param>
    /// <returns>The resulting scheduled cron job</returns>
    ScheduledCronJob Create(string cronExpression, string prompt, bool recurring);

    /// <summary>
    /// Deletes value
    /// </summary>
    /// <param name="id">The id</param>
    /// <returns>A value indicating whether the operation succeeded</returns>
    bool Delete(string id);

    /// <summary>
    /// Lists value
    /// </summary>
    /// <returns>The resulting i read only list scheduled cron job</returns>
    IReadOnlyList<ScheduledCronJob> List();

    /// <summary>
    /// Gets the count
    /// </summary>
    int Count { get; }

    /// <summary>
    /// Gets a value indicating whether is running
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// Starts value
    /// </summary>
    /// <param name="onFire">The on fire</param>
    void Start(Action<ScheduledCronJob> onFire);

    /// <summary>
    /// Stops value
    /// </summary>
    void Stop();

    /// <summary>
    /// Executes tick
    /// </summary>
    /// <param name="now">The now</param>
    void Tick(DateTimeOffset? now = null);

    /// <summary>
    /// Gets exit summary
    /// </summary>
    /// <returns>The resulting string?</returns>
    string? GetExitSummary();

    /// <summary>
    /// Executes destroy
    /// </summary>
    void Destroy();
}
