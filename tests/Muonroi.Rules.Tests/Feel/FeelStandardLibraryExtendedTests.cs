namespace Muonroi.Rules.Tests.Feel;

public class FeelStandardLibraryExtendedTests
{
    [Fact]
    public void TryCall_Time_FromString_ShouldParse()
    {
        FeelStandardLibrary.TryCall("time", new object?[] { "14:30:00" }, out object? result);
        ((TimeOnly)result!).Should().Be(new TimeOnly(14, 30, 0));
    }

    [Fact]
    public void TryCall_Time_FromComponents_ShouldCreate()
    {
        FeelStandardLibrary.TryCall("time", new object?[] { 14.0, 30.0, 0.0 }, out object? result);
        ((TimeOnly)result!).Should().Be(new TimeOnly(14, 30, 0));
    }

    [Fact]
    public void TryCall_DateAndTime_FromString_ShouldParse()
    {
        FeelStandardLibrary.TryCall("date_and_time",
            new object?[] { "2026-03-20T14:30:00" }, out object? result);
        result.Should().BeOfType<DateTime>();
    }

    [Fact]
    public void TryCall_DateAndTime_FromComponents_ShouldCreate()
    {
        FeelStandardLibrary.TryCall("date_and_time",
            new object?[] { "2026-03-20", "14:30:00" }, out object? result);
        result.Should().BeOfType<DateTime>();
    }

    [Fact]
    public void TryCall_Sublist_WithLength_ShouldReturnSubset()
    {
        FeelStandardLibrary.TryCall("sublist",
            new object?[] { new object[] { "a", "b", "c", "d" }, 2.0, 2.0 }, out object? result);
        ((object?[])result!).Should().BeEquivalentTo(new[] { "b", "c" });
    }

    [Fact]
    public void TryCall_Sublist_WithoutLength_ShouldReturnFromStart()
    {
        FeelStandardLibrary.TryCall("sublist",
            new object?[] { new object[] { "a", "b", "c" }, 2.0 }, out object? result);
        ((object?[])result!).Should().BeEquivalentTo(new[] { "b", "c" });
    }

    [Fact]
    public void TryCall_Concatenate_ShouldMergeLists()
    {
        FeelStandardLibrary.TryCall("concatenate",
            new object?[] { new object[] { 1, 2 }, new object[] { 3, 4 } }, out object? result);
        ((object?[])result!).Should().BeEquivalentTo(new object[] { 1, 2, 3, 4 });
    }

    [Fact]
    public void TryCall_InsertBefore_ShouldInsertAtPosition()
    {
        FeelStandardLibrary.TryCall("insert_before",
            new object?[] { new object[] { "a", "c" }, 2.0, "b" }, out object? result);
        ((object?[])result!).Should().ContainInOrder("a", "b", "c");
    }

    [Fact]
    public void TryCall_Remove_ShouldRemoveAtPosition()
    {
        FeelStandardLibrary.TryCall("remove",
            new object?[] { new object[] { "a", "b", "c" }, 2.0 }, out object? result);
        ((object?[])result!).Should().BeEquivalentTo(new[] { "a", "c" });
    }

    [Fact]
    public void TryCall_Union_ShouldCombineAndDeduplicate()
    {
        FeelStandardLibrary.TryCall("union",
            new object?[] { new object[] { 1, 2 }, new object[] { 2, 3 } }, out object? result);
        ((object?[])result!).Should().BeEquivalentTo(new object[] { 1, 2, 3 });
    }

    [Fact]
    public void TryCall_Sort_ShouldOrderList()
    {
        FeelStandardLibrary.TryCall("sort",
            new object?[] { new object[] { 3, 1, 2 } }, out object? result);
        ((object?[])result!).Should().ContainInOrder(1, 2, 3);
    }

    [Fact]
    public void TryCall_Substring_BeyondLength_ShouldReturnEmpty()
    {
        FeelStandardLibrary.TryCall("substring",
            new object?[] { "hi", 10.0 }, out object? result);
        result.Should().Be(string.Empty);
    }

    [Fact]
    public void TryCall_SubstringBefore_NoMatch_ShouldReturnEmpty()
    {
        FeelStandardLibrary.TryCall("substring_before",
            new object?[] { "hello", "xyz" }, out object? result);
        result.Should().Be(string.Empty);
    }

    [Fact]
    public void TryCall_SubstringAfter_NoMatch_ShouldReturnEmpty()
    {
        FeelStandardLibrary.TryCall("substring_after",
            new object?[] { "hello", "xyz" }, out object? result);
        result.Should().Be(string.Empty);
    }

    [Fact]
    public void TryCall_Replace_WithFlags_ShouldBeCaseInsensitive()
    {
        FeelStandardLibrary.TryCall("replace",
            new object?[] { "Hello WORLD", "hello", "hi", "i" }, out object? result);
        result.Should().Be("hi WORLD");
    }

    [Fact]
    public void TryCall_Matches_WithFlags_ShouldBeCaseInsensitive()
    {
        FeelStandardLibrary.TryCall("matches",
            new object?[] { "Hello", "hello", "i" }, out object? result);
        result.Should().Be(true);
    }

    [Fact]
    public void TryCall_Contains_CaseInsensitive_ShouldReturnTrue()
    {
        FeelStandardLibrary.TryCall("contains",
            new object?[] { "Hello World", "HELLO" }, out object? result);
        result.Should().Be(true);
    }

    [Fact]
    public void TryCall_Mean_EmptyList_ShouldReturnZero()
    {
        FeelStandardLibrary.TryCall("mean",
            new object?[] { Array.Empty<object>() }, out object? result);
        ((double)result!).Should().Be(0d);
    }

    [Fact]
    public void TryCall_ListContains_NotFound_ShouldReturnFalse()
    {
        FeelStandardLibrary.TryCall("list_contains",
            new object?[] { new object[] { 1, 2, 3 }, 5 }, out object? result);
        result.Should().Be(false);
    }

    [Fact]
    public void TryCall_Years_ShouldReturnDays()
    {
        FeelStandardLibrary.TryCall("years",
            new object?[] { 2.0 }, out object? result);
        ((TimeSpan)result!).TotalDays.Should().Be(730.0);
    }

    [Fact]
    public void TryCall_Log_ShouldReturnNaturalLog()
    {
        FeelStandardLibrary.TryCall("log",
            new object?[] { Math.E }, out object? result);
        ((double)result!).Should().BeApproximately(1.0, 0.001);
    }

    [Fact]
    public void TryCall_Exp_ShouldReturnExponential()
    {
        FeelStandardLibrary.TryCall("exp",
            new object?[] { 1.0 }, out object? result);
        ((double)result!).Should().BeApproximately(Math.E, 0.001);
    }

    [Fact]
    public void TryCall_DayOfWeek_ShouldReturnDayName()
    {
        // 2026-03-20 is a Friday
        FeelStandardLibrary.TryCall("day_of_week",
            new object?[] { "2026-03-20" }, out object? result);
        result.Should().Be("Friday");
    }

    [Fact]
    public void TryCall_DayOfYear_ShouldReturnDayNumber()
    {
        FeelStandardLibrary.TryCall("day_of_year",
            new object?[] { "2026-01-15" }, out object? result);
        ((double)result!).Should().Be(15.0);
    }

    [Fact]
    public void TryCall_MonthOfYear_ShouldReturnMonth()
    {
        FeelStandardLibrary.TryCall("month_of_year",
            new object?[] { "2026-03-20" }, out object? result);
        ((double)result!).Should().Be(3.0);
    }

    [Fact]
    public void TryCall_Year_ShouldReturnYear()
    {
        FeelStandardLibrary.TryCall("year",
            new object?[] { "2026-03-20" }, out object? result);
        ((double)result!).Should().Be(2026.0);
    }

    [Fact]
    public void TryCall_WeekOfYear_ShouldReturnWeek()
    {
        FeelStandardLibrary.TryCall("week_of_year",
            new object?[] { "2026-01-15" }, out object? result);
        result.Should().BeOfType<double>();
    }

    [Fact]
    public void TryCall_Is_SameValues_ShouldReturnTrue()
    {
        FeelStandardLibrary.TryCall("is",
            new object?[] { 42, 42 }, out object? result);
        result.Should().Be(true);
    }

    [Fact]
    public void TryCall_GetEntries_NullableDict_ShouldReturnEntries()
    {
        var dict = new Dictionary<string, object?> { ["a"] = 1, ["b"] = null };
        FeelStandardLibrary.TryCall("get_entries",
            new object?[] { dict }, out object? result);
        var entries = (Dictionary<string, object?>[])result!;
        entries.Should().HaveCount(2);
    }

    [Fact]
    public void TryCall_GetValue_NullableDict_ShouldReturnValue()
    {
        var dict = new Dictionary<string, object?> { ["name"] = "test" };
        FeelStandardLibrary.TryCall("get_value",
            new object?[] { dict, "name" }, out object? result);
        result.Should().Be("test");
    }

    [Fact]
    public void TryCall_Flatten_NullInput_ShouldReturnEmpty()
    {
        FeelStandardLibrary.TryCall("flatten",
            new object?[] { null }, out object? result);
        ((object?[])result!).Should().BeEmpty();
    }

    [Fact]
    public void TryCall_Flatten_StringInput_ShouldReturnSingle()
    {
        FeelStandardLibrary.TryCall("flatten",
            new object?[] { "hello" }, out object? result);
        ((object?[])result!).Should().ContainSingle("hello");
    }

    [Fact]
    public void TryCall_AsDouble_VariousTypes()
    {
        // Test via floor function which uses AsDouble internally
        FeelStandardLibrary.TryCall("floor", new object?[] { 3.7f }, out object? r1);
        ((double)r1!).Should().Be(3.0);

        FeelStandardLibrary.TryCall("floor", new object?[] { 3.7m }, out object? r2);
        ((double)r2!).Should().Be(3.0);

        FeelStandardLibrary.TryCall("floor", new object?[] { 3 }, out object? r3);
        ((double)r3!).Should().Be(3.0);

        FeelStandardLibrary.TryCall("floor", new object?[] { 3L }, out object? r4);
        ((double)r4!).Should().Be(3.0);

        FeelStandardLibrary.TryCall("floor", new object?[] { "3.7" }, out object? r5);
        ((double)r5!).Should().Be(3.0);

        FeelStandardLibrary.TryCall("floor", new object?[] { null }, out object? r6);
        ((double)r6!).Should().Be(0.0);
    }

    [Fact]
    public void TryCall_DaysAndTimeDuration_ShouldParseLikeDuration()
    {
        FeelStandardLibrary.TryCall("days_and_time_duration",
            new object?[] { "PT1H" }, out object? result);
        ((TimeSpan)result!).Should().Be(TimeSpan.FromHours(1));
    }

    [Fact]
    public void TryCall_All_ShouldReturnTrueWhenAllItemsAreTruthy()
    {
        FeelStandardLibrary.TryCall("all",
            new object?[] { new object?[] { true, 1, "x" } }, out object? result);
        result.Should().Be(true);
    }

    [Fact]
    public void TryCall_Any_ShouldReturnFalseWhenNoItemsAreTruthy()
    {
        FeelStandardLibrary.TryCall("any",
            new object?[] { new object?[] { false, 0, null, "" } }, out object? result);
        result.Should().Be(false);
    }

    [Fact]
    public void TryCall_Append_ShouldAddItemToList()
    {
        FeelStandardLibrary.TryCall("append",
            new object?[] { new object[] { "a", "b" }, "c" }, out object? result);
        ((object?[])result!).Should().ContainInOrder("a", "b", "c");
    }

    [Fact]
    public void TryCall_IndexOf_ShouldReturnAllMatchingIndexes()
    {
        FeelStandardLibrary.TryCall("index_of",
            new object?[] { new object[] { "a", "b", "a" }, "a" }, out object? result);
        ((double[])result!).Should().Equal(1.0, 3.0);
    }

    [Fact]
    public void TryCall_DistinctValues_ShouldRemoveDuplicates()
    {
        FeelStandardLibrary.TryCall("distinct_values",
            new object?[] { new object[] { 1, 1, 2, 2, 3 } }, out object? result);
        ((object?[])result!).Should().BeEquivalentTo(new object[] { 1, 2, 3 });
    }

    [Fact]
    public void TryCall_Split_ShouldSplitUsingDelimiter()
    {
        FeelStandardLibrary.TryCall("split",
            new object?[] { "a|b|c", "|" }, out object? result);
        ((string[])result!).Should().Equal("a", "b", "c");
    }

    [Fact]
    public void TryCall_StartsWith_AndEndsWith_ShouldIgnoreCase()
    {
        FeelStandardLibrary.TryCall("starts_with",
            new object?[] { "Hello", "he" }, out object? startsWith);
        FeelStandardLibrary.TryCall("ends_with",
            new object?[] { "Hello", "LO" }, out object? endsWith);
        startsWith.Should().Be(true);
        endsWith.Should().Be(true);
    }

    [Fact]
    public void TryCall_Date_FromComponents_ShouldCreateDate()
    {
        FeelStandardLibrary.TryCall("date",
            new object?[] { 2026.0, 3.0, 21.0 }, out object? result);
        result.Should().Be(new DateOnly(2026, 3, 21));
    }

    [Fact]
    public void TryCall_YearsAndMonthsDuration_ShouldReturnMonthDifference()
    {
        FeelStandardLibrary.TryCall("years_and_months_duration",
            new object?[] { "2025-01-01", "2026-03-01" }, out object? result);
        result.Should().Be(14);
    }

    [Fact]
    public void TryCall_GetEntries_NonNullableDict_ShouldReturnEntries()
    {
        var dict = new Dictionary<string, object> { ["a"] = 1, ["b"] = "x" };

        FeelStandardLibrary.TryCall("get_entries",
            new object?[] { dict }, out object? result);

        var entries = (Dictionary<string, object?>[])result!;
        entries.Should().HaveCount(2);
        entries.Should().ContainSingle(x => Equals(x["key"], "a") && Equals(x["value"], 1));
    }

    [Fact]
    public void TryCall_GetValue_MissingKey_ShouldReturnNull()
    {
        var dict = new Dictionary<string, object> { ["name"] = "test" };

        FeelStandardLibrary.TryCall("get_value",
            new object?[] { dict, "missing" }, out object? result);

        result.Should().BeNull();
    }

    [Fact]
    public void TryCall_All_ShouldReturnTrueWhenEveryItemTruthy()
    {
        FeelStandardLibrary.TryCall("all",
            new object?[] { new object?[] { true, 1, "yes" } }, out object? result);

        result.Should().Be(true);
    }

    [Fact]
    public void TryCall_Any_ShouldReturnFalseWhenEveryItemFalsy()
    {
        FeelStandardLibrary.TryCall("any",
            new object?[] { new object?[] { false, 0, "" } }, out object? result);

        result.Should().Be(false);
    }

    [Fact]
    public void TryCall_Days_ShouldReturnTimeSpan()
    {
        FeelStandardLibrary.TryCall("days",
            new object?[] { 3.0 }, out object? result);

        ((TimeSpan)result!).Should().Be(TimeSpan.FromDays(3));
    }

    [Fact]
    public void TryCall_Today_AndNow_ShouldReturnCurrentUtcValues()
    {
        FeelStandardLibrary.TryCall("today", Array.Empty<object?>(), out object? today);
        FeelStandardLibrary.TryCall("now", Array.Empty<object?>(), out object? now);

        today.Should().BeOfType<DateTime>();
        ((DateTime)today!).Should().BeCloseTo(DateTime.UtcNow.Date, TimeSpan.FromDays(1));
        now.Should().BeOfType<DateTime>();
        ((DateTime)now!).Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }
}
