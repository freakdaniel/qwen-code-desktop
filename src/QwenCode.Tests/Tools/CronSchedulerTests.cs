using QwenCode.App.Models;
using QwenCode.App.Tools;

namespace QwenCode.Tests.Tools;

public sealed class CronSchedulerTests
{
    [Fact]
    public void InMemoryCronScheduler_CreateAndList_TracksSessionOnlyJobs()
    {
        var scheduler = new InMemoryCronScheduler();

        var recurring = scheduler.Create("*/5 * * * *", "check build", recurring: true);
        var oneShot = scheduler.Create("30 10 * * *", "ping team", recurring: false);
        var jobs = scheduler.List();

        Assert.Equal(2, scheduler.Count);
        Assert.Contains(jobs, job => job.Id == recurring.Id && job.IsRecurring);
        Assert.Contains(jobs, job => job.Id == oneShot.Id && !job.IsRecurring);
        Assert.True(recurring.ExpiresAt > recurring.CreatedAt);
        Assert.Equal(DateTimeOffset.MaxValue, oneShot.ExpiresAt);
    }

    [Fact]
    public void InMemoryCronScheduler_Delete_RemovesExistingJob()
    {
        var scheduler = new InMemoryCronScheduler();
        var job = scheduler.Create("*/5 * * * *", "check build", recurring: true);

        var deleted = scheduler.Delete(job.Id);
        var secondDelete = scheduler.Delete(job.Id);

        Assert.True(deleted);
        Assert.False(secondDelete);
        Assert.Empty(scheduler.List());
    }

    [Fact]
    public void InMemoryCronScheduler_Tick_FiresOneShotJobOnlyOnce()
    {
        var scheduler = new InMemoryCronScheduler();
        var firedJobs = new List<ScheduledCronJob>();
        scheduler.Start(firedJobs.Add);

        try
        {
            var job = scheduler.Create("* * * * *", "every minute", recurring: false);
            var firstFire = new DateTimeOffset(2026, 4, 2, 10, 1, 1, TimeSpan.Zero);

            scheduler.Tick(firstFire);
            scheduler.Tick(firstFire.AddSeconds(30));

            Assert.Single(firedJobs);
            Assert.Equal(job.Id, firedJobs[0].Id);
            Assert.Empty(scheduler.List());
        }
        finally
        {
            scheduler.Destroy();
        }
    }

    [Fact]
    public void InMemoryCronScheduler_GetExitSummary_DescribesRemainingJobs()
    {
        var scheduler = new InMemoryCronScheduler();
        scheduler.Create("*/5 * * * *", "check build", recurring: true);
        scheduler.Create("0 */2 * * *", "ping", recurring: true);

        var summary = scheduler.GetExitSummary();

        Assert.NotNull(summary);
        Assert.Contains("active loops cancelled", summary);
        Assert.Contains("Every 5 minutes", summary);
        Assert.Contains("Every 2 hours", summary);
    }
}
