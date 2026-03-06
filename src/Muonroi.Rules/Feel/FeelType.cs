namespace Muonroi.Rules.Feel;

public enum FeelType
{
    Number,
    String,
    Boolean,
    Date,
    Time,
    DateTime,
    Duration,
    YearMonthDuration,
    DayTimeDuration,
    List,
    Context,
    Range,
    Function,
    Null
}

public abstract record FeelValue
{
    public abstract FeelType Type { get; }
    public abstract object? Value { get; }
}

public sealed record FeelNull : FeelValue
{
    public override FeelType Type => FeelType.Null;
    public override object? Value => null;
}

public sealed record FeelNumber(double Number) : FeelValue
{
    public override FeelType Type => FeelType.Number;
    public override object? Value => Number;
}

public sealed record FeelString(string Text) : FeelValue
{
    public override FeelType Type => FeelType.String;
    public override object? Value => Text;
}

public sealed record FeelBoolean(bool Bool) : FeelValue
{
    public override FeelType Type => FeelType.Boolean;
    public override object? Value => Bool;
}

public sealed record FeelDate(DateOnly Date) : FeelValue
{
    public override FeelType Type => FeelType.Date;
    public override object? Value => Date;
}

public sealed record FeelTime(TimeOnly Time) : FeelValue
{
    public override FeelType Type => FeelType.Time;
    public override object? Value => Time;
}

public sealed record FeelDateTime(DateTime DateTime) : FeelValue
{
    public override FeelType Type => FeelType.DateTime;
    public override object? Value => DateTime;
}

public sealed record FeelDuration(TimeSpan Duration) : FeelValue
{
    public override FeelType Type => FeelType.Duration;
    public override object? Value => Duration;
}

public sealed record FeelList(IReadOnlyList<FeelValue> Items) : FeelValue
{
    public override FeelType Type => FeelType.List;
    public override object? Value => Items;
}

public sealed record FeelContext(IReadOnlyDictionary<string, FeelValue> Entries) : FeelValue
{
    public override FeelType Type => FeelType.Context;
    public override object? Value => Entries;
}

public sealed record FeelRange(FeelValue Start, FeelValue End, bool IncludeStart, bool IncludeEnd) : FeelValue
{
    public override FeelType Type => FeelType.Range;
    public override object? Value => (Start, End, IncludeStart, IncludeEnd);
}
