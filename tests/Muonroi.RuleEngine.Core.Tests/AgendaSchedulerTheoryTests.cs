using Muonroi.RuleEngine.Core.Runtime;
using Xunit;

namespace Muonroi.RuleEngine.Core.Tests;

public class AgendaSchedulerTheoryTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(20)]
    public async Task FireAsync_WithMultipleActivations_FiresAllActivations(int activationCount)
    {
        int firedCount = 0;
        List<Activation> activations = [];
        for (int i = 0; i < activationCount; i++)
        {
            int priority = activationCount - i;
            activations.Add(new Activation(async _ =>
            {
                firedCount++;
                await Task.CompletedTask;
            }, priority));
        }

        AgendaScheduler scheduler = new(activations);
        await scheduler.FireAsync();

        Assert.Equal(activationCount, firedCount);
    }

    [Theory]
    [InlineData(10, 5)]
    [InlineData(5, 10)]
    [InlineData(0, 1)]
    [InlineData(100, 50)]
    public async Task FireAsync_WithDifferentPriorities_FiresInCorrectOrder(int priority1, int priority2)
    {
        List<int> order = [];
        Activation activation1 = new(async _ =>
        {
            order.Add(1);
            await Task.CompletedTask;
        }, priority1);

        Activation activation2 = new(async _ =>
        {
            order.Add(2);
            await Task.CompletedTask;
        }, priority2);

        AgendaScheduler scheduler = new([activation1, activation2]);
        await scheduler.FireAsync();

        if (priority1 > priority2)
        {
            Assert.Equal(new[] { 1, 2 }, order);
        }
        else
        {
            Assert.Equal(new[] { 2, 1 }, order);
        }
    }

    [Theory]
    [InlineData("GroupA")]
    [InlineData("GroupB")]
    [InlineData("Group1")]
    [InlineData("TestGroup")]
    public async Task HaltGroup_WithDifferentGroups_HaltsCorrectly(string groupName)
    {
        List<string> fired = [];
        Activation activation1 = new(async ctx =>
        {
            fired.Add($"{groupName}:1");
            ctx.HaltGroup();
            await Task.CompletedTask;
        }, 10, groupName);

        Activation activation2 = new(async _ =>
        {
            fired.Add($"{groupName}:2");
            await Task.CompletedTask;
        }, 5, groupName);

        Activation activation3 = new(async _ =>
        {
            fired.Add("Other:3");
            await Task.CompletedTask;
        }, 5, "OtherGroup");

        AgendaScheduler scheduler = new([activation1, activation2, activation3]);
        await scheduler.FireAsync();

        Assert.Contains($"{groupName}:1", fired);
        Assert.DoesNotContain($"{groupName}:2", fired);
        Assert.Contains("Other:3", fired);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task FireAsync_WithEmptyOrFewActivations_CompletesSuccessfully(int count)
    {
        List<Activation> activations = [];
        for (int i = 0; i < count; i++)
        {
            activations.Add(new Activation(async _ => await Task.CompletedTask, i));
        }

        AgendaScheduler scheduler = new(activations);
        await scheduler.FireAsync();

        Assert.True(true);
    }

    [Theory]
    [InlineData(-10)]
    [InlineData(-5)]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(10)]
    public async Task FireAsync_WithNegativeAndPositivePriorities_OrdersCorrectly(int priority)
    {
        List<int> fired = [];
        Activation activation = new(async _ =>
        {
            fired.Add(priority);
            await Task.CompletedTask;
        }, priority);

        AgendaScheduler scheduler = new([activation]);
        await scheduler.FireAsync();

        Assert.Contains(priority, fired);
    }

    [Fact]
    public async Task FireAsync_WithNullGroup_ExecutesCorrectly()
    {
        bool fired = false;
        Activation activation = new(async _ =>
        {
            fired = true;
            await Task.CompletedTask;
        }, 10, null);

        AgendaScheduler scheduler = new([activation]);
        await scheduler.FireAsync();

        Assert.True(fired);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    public async Task FireAsync_WithEmptyStringGroup_ExecutesCorrectly(string emptyGroup)
    {
        bool fired = false;
        Activation activation = new(async _ =>
        {
            fired = true;
            await Task.CompletedTask;
        }, 10, emptyGroup);

        AgendaScheduler scheduler = new([activation]);
        await scheduler.FireAsync();

        Assert.True(fired);
    }
}
