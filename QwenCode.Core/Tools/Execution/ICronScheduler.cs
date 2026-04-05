using QwenCode.App.Models;

namespace QwenCode.App.Tools;

public interface ICronScheduler
{
    ScheduledCronJob Create(string cronExpression, string prompt, bool recurring);

    bool Delete(string id);

    IReadOnlyList<ScheduledCronJob> List();

    int Count { get; }

    bool IsRunning { get; }

    void Start(Action<ScheduledCronJob> onFire);

    void Stop();

    void Tick(DateTimeOffset? now = null);

    string? GetExitSummary();

    void Destroy();
}
