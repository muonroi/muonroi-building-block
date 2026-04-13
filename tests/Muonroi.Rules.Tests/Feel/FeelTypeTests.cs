namespace Muonroi.Rules.Tests.Feel;

public class FeelTypeTests
{
    [Fact]
    public void FeelNull_ShouldHaveCorrectTypeAndValue()
    {
        var value = new FeelNull();

        value.Type.Should().Be(FeelType.Null);
        value.Value.Should().BeNull();
    }

    [Fact]
    public void FeelNumber_ShouldHaveCorrectTypeAndValue()
    {
        var value = new FeelNumber(42.5);

        value.Type.Should().Be(FeelType.Number);
        value.Value.Should().Be(42.5);
        value.Number.Should().Be(42.5);
    }

    [Fact]
    public void FeelString_ShouldHaveCorrectTypeAndValue()
    {
        var value = new FeelString("hello");

        value.Type.Should().Be(FeelType.String);
        value.Value.Should().Be("hello");
        value.Text.Should().Be("hello");
    }

    [Fact]
    public void FeelBoolean_True_ShouldHaveCorrectTypeAndValue()
    {
        var value = new FeelBoolean(true);

        value.Type.Should().Be(FeelType.Boolean);
        value.Value.Should().Be(true);
        value.Bool.Should().BeTrue();
    }

    [Fact]
    public void FeelBoolean_False_ShouldHaveCorrectTypeAndValue()
    {
        var value = new FeelBoolean(false);

        value.Type.Should().Be(FeelType.Boolean);
        value.Value.Should().Be(false);
    }

    [Fact]
    public void FeelDate_ShouldHaveCorrectTypeAndValue()
    {
        var date = new DateOnly(2026, 3, 20);
        var value = new FeelDate(date);

        value.Type.Should().Be(FeelType.Date);
        value.Value.Should().Be(date);
        value.Date.Should().Be(date);
    }

    [Fact]
    public void FeelTime_ShouldHaveCorrectTypeAndValue()
    {
        var time = new TimeOnly(14, 30, 0);
        var value = new FeelTime(time);

        value.Type.Should().Be(FeelType.Time);
        value.Value.Should().Be(time);
        value.Time.Should().Be(time);
    }

    [Fact]
    public void FeelDateTime_ShouldHaveCorrectTypeAndValue()
    {
        var dt = new DateTime(2026, 3, 20, 14, 30, 0, DateTimeKind.Utc);
        var value = new FeelDateTime(dt);

        value.Type.Should().Be(FeelType.DateTime);
        value.Value.Should().Be(dt);
    }

    [Fact]
    public void FeelDuration_ShouldHaveCorrectTypeAndValue()
    {
        var duration = TimeSpan.FromHours(2);
        var value = new FeelDuration(duration);

        value.Type.Should().Be(FeelType.Duration);
        value.Value.Should().Be(duration);
    }

    [Fact]
    public void FeelList_ShouldHaveCorrectTypeAndValue()
    {
        var items = new FeelValue[] { new FeelNumber(1), new FeelNumber(2) };
        var value = new FeelList(items);

        value.Type.Should().Be(FeelType.List);
        value.Items.Should().HaveCount(2);
        value.Value.Should().BeSameAs(items);
    }

    [Fact]
    public void FeelContext_ShouldHaveCorrectTypeAndValue()
    {
        var entries = new Dictionary<string, FeelValue>
        {
            ["name"] = new FeelString("test"),
            ["count"] = new FeelNumber(5)
        };
        var value = new FeelContext(entries);

        value.Type.Should().Be(FeelType.Context);
        value.Entries.Should().HaveCount(2);
        value.Value.Should().BeSameAs(entries);
    }

    [Fact]
    public void FeelRange_ShouldHaveCorrectTypeAndValue()
    {
        var value = new FeelRange(new FeelNumber(1), new FeelNumber(10), true, false);

        value.Type.Should().Be(FeelType.Range);
        value.Start.Should().BeOfType<FeelNumber>();
        value.End.Should().BeOfType<FeelNumber>();
        value.IncludeStart.Should().BeTrue();
        value.IncludeEnd.Should().BeFalse();
        value.Value.Should().Be((value.Start, value.End, value.IncludeStart, value.IncludeEnd));
    }

    [Fact]
    public void FeelType_Enum_ShouldHaveAllExpectedValues()
    {
        Enum.GetValues<FeelType>().Should().HaveCount(14);
    }

    [Fact]
    public void FeelValue_RecordEquality_ShouldWork()
    {
        var a = new FeelNumber(42);
        var b = new FeelNumber(42);
        a.Should().Be(b);

        var c = new FeelString("hello");
        var d = new FeelString("hello");
        c.Should().Be(d);
    }
}
