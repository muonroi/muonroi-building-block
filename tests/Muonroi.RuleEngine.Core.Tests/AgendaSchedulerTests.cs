using Muonroi.RuleEngine.Core.Runtime;
using Xunit;

namespace Muonroi.RuleEngine.Core.Tests;

public class AgendaSchedulerTests
{
    [Fact]
    public async Task FiresByPriority()
    {
        List<int> order = [];
        Activation r1 = new(async _ =>
        {
            order.Add(1);
            await Task.CompletedTask;
        }, 10);
        Activation r2 = new(async _ =>
        {
            order.Add(2);
            await Task.CompletedTask;
        }, 5);
        AgendaScheduler scheduler = new([r1, r2]);

        await scheduler.FireAsync();

        Assert.Equal(new[] { 1, 2 }, order);
    }

    [Fact]
    public async Task HaltGroup_SkipsRemainingInGroup()
    {
        List<int> fired = [];
        Activation r1 = new(async ctx =>
        {
            fired.Add(1);
            ctx.HaltGroup();
            await Task.CompletedTask;
        }, 10, "A");
        Activation r2 = new(async _ =>
        {
            fired.Add(2);
            await Task.CompletedTask;
        }, 5, "A");
        Activation r3 = new(async _ =>
        {
            fired.Add(3);
            await Task.CompletedTask;
        }, 5, "B");
        AgendaScheduler scheduler = new([r1, r2, r3]);

        await scheduler.FireAsync();

        Assert.Equal(new[] { 1, 3 }, fired);
    }
}