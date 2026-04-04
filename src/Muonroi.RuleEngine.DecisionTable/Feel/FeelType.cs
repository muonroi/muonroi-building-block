namespace Muonroi.RuleEngine.DecisionTable.Feel;

/// <summary>
/// Supported FEEL data types.
/// </summary>
public enum FeelType
{
    /// <summary>Numeric value.</summary>
    Number,
    /// <summary>String value.</summary>
    String,
    /// <summary>Boolean value.</summary>
    Boolean,
    /// <summary>Date only value.</summary>
    Date,
    /// <summary>Time only value.</summary>
    Time,
    /// <summary>Date and time value.</summary>
    DateTime,
    /// <summary>Time span duration.</summary>
    Duration,
    /// <summary>ISO 8601 year-month duration.</summary>
    YearMonthDuration,
    /// <summary>ISO 8601 day-time duration.</summary>
    DayTimeDuration,
    /// <summary>List of values.</summary>
    List,
    /// <summary>Key-value context.</summary>
    Context,
    /// <summary>Range of values.</summary>
    Range,
    /// <summary>Function definition.</summary>
    Function,
    /// <summary>Null value.</summary>
    Null
}

/// <summary>
/// Base record for all FEEL values.
/// </summary>
public abstract record FeelValue
{
    /// <summary>Gets the FEEL type of the value.</summary>
    public abstract FeelType Type { get; }
    /// <summary>Gets the underlying .NET value.</summary>
    public abstract object? Value { get; }
}

/// <summary>
/// Represents a FEEL null value.
/// </summary>
public sealed record FeelNull : FeelValue
{
    /// <inheritdoc />
    public override FeelType Type => FeelType.Null;
    /// <inheritdoc />
    public override object? Value => null;
}

/// <summary>
/// Represents a FEEL numeric value.
/// </summary>
/// <param name="Number">The underlying numeric value.</param>
public sealed record FeelNumber(double Number) : FeelValue
{
    /// <inheritdoc />
    public override FeelType Type => FeelType.Number;
    /// <inheritdoc />
    public override object? Value => Number;
}

/// <summary>
/// Represents a FEEL string value.
/// </summary>
/// <param name="Text">The underlying string text.</param>
public sealed record FeelString(string Text) : FeelValue
{
    /// <inheritdoc />
    public override FeelType Type => FeelType.String;
    /// <inheritdoc />
    public override object? Value => Text;
}

/// <summary>
/// Represents a FEEL boolean value.
/// </summary>
/// <param name="Bool">The underlying boolean value.</param>
public sealed record FeelBoolean(bool Bool) : FeelValue
{
    /// <inheritdoc />
    public override FeelType Type => FeelType.Boolean;
    /// <inheritdoc />
    public override object? Value => Bool;
}

/// <summary>
/// Represents a FEEL date value.
/// </summary>
/// <param name="Date">The underlying date value.</param>
public sealed record FeelDate(DateOnly Date) : FeelValue
{
    /// <inheritdoc />
    public override FeelType Type => FeelType.Date;
    /// <inheritdoc />
    public override object? Value => Date;
}

/// <summary>
/// Represents a FEEL time value.
/// </summary>
/// <param name="Time">The underlying time value.</param>
public sealed record FeelTime(TimeOnly Time) : FeelValue
{
    /// <inheritdoc />
    public override FeelType Type => FeelType.Time;
    /// <inheritdoc />
    public override object? Value => Time;
}

/// <summary>
/// Represents a FEEL date-time value.
/// </summary>
/// <param name="DateTime">The underlying date-time value.</param>
public sealed record FeelDateTime(DateTime DateTime) : FeelValue
{
    /// <inheritdoc />
    public override FeelType Type => FeelType.DateTime;
    /// <inheritdoc />
    public override object? Value => DateTime;
}

/// <summary>
/// Represents a FEEL duration value.
/// </summary>
/// <param name="Duration">The underlying duration value.</param>
public sealed record FeelDuration(TimeSpan Duration) : FeelValue
{
    /// <inheritdoc />
    public override FeelType Type => FeelType.Duration;
    /// <inheritdoc />
    public override object? Value => Duration;
}

/// <summary>
/// Represents a FEEL list value.
/// </summary>
/// <param name="Items">The items in the list.</param>
public sealed record FeelList(IReadOnlyList<FeelValue> Items) : FeelValue
{
    /// <inheritdoc />
    public override FeelType Type => FeelType.List;
    /// <inheritdoc />
    public override object? Value => Items;
}

/// <summary>
/// Represents a FEEL context (dictionary) value.
/// </summary>
/// <param name="Entries">The entries in the context.</param>
public sealed record FeelContext(IReadOnlyDictionary<string, FeelValue> Entries) : FeelValue
{
    /// <inheritdoc />
    public override FeelType Type => FeelType.Context;
    /// <inheritdoc />
    public override object? Value => Entries;
}

/// <summary>
/// Represents a FEEL range value.
/// </summary>
/// <param name="Start">The start value of the range.</param>
/// <param name="End">The end value of the range.</param>
/// <param name="IncludeStart">Whether the start value is included.</param>
/// <param name="IncludeEnd">Whether the end value is included.</param>
public sealed record FeelRange(FeelValue Start, FeelValue End, bool IncludeStart, bool IncludeEnd) : FeelValue
{
    /// <inheritdoc />
    public override FeelType Type => FeelType.Range;
    /// <inheritdoc />
    public override object? Value => (Start, End, IncludeStart, IncludeEnd);
}
