using Muonroi.RuleEngine.Core;
using Xunit;

namespace Muonroi.RuleEngine.Core.Tests;

public class FactBagTheoryTests
{
    [Theory]
    [InlineData("key1", "value1")]
    [InlineData("key2", "value2")]
    [InlineData("testKey", "testValue")]
    [InlineData("data", "content")]
    public void FactBag_SetAndGet_StringValues_WorksCorrectly(string key, string value)
    {
        FactBag bag = new();
        bag[key] = value;

        Assert.Equal(value, bag[key]);
    }

    [Theory]
    [InlineData("intKey", 42)]
    [InlineData("number", 100)]
    [InlineData("count", 0)]
    [InlineData("negative", -50)]
    public void FactBag_SetAndGet_IntegerValues_WorksCorrectly(string key, int value)
    {
        FactBag bag = new();
        bag[key] = value;

        Assert.Equal(value, bag[key]);
    }

    [Theory]
    [InlineData("boolKey", true)]
    [InlineData("flag", false)]
    [InlineData("isActive", true)]
    [InlineData("isDeleted", false)]
    public void FactBag_SetAndGet_BooleanValues_WorksCorrectly(string key, bool value)
    {
        FactBag bag = new();
        bag[key] = value;

        Assert.Equal(value, bag[key]);
    }

    [Theory]
    [InlineData("doubleKey", 3.14)]
    [InlineData("pi", 3.14159)]
    [InlineData("rate", 0.05)]
    [InlineData("percentage", 99.9)]
    public void FactBag_SetAndGet_DoubleValues_WorksCorrectly(string key, double value)
    {
        FactBag bag = new();
        bag[key] = value;

        Assert.Equal(value, bag[key]);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    public void FactBag_EmptyStringKey_StillWorks(string emptyKey)
    {
        FactBag bag = new();
        bag[emptyKey] = "value";

        Assert.Equal("value", bag[emptyKey]);
    }

    [Theory]
    [InlineData("key@special")]
    [InlineData("key#hash")]
    [InlineData("key$dollar")]
    [InlineData("key:colon")]
    [InlineData("key.dot")]
    public void FactBag_SpecialCharactersInKey_WorksCorrectly(string specialKey)
    {
        FactBag bag = new();
        bag[specialKey] = "special-value";

        Assert.Equal("special-value", bag[specialKey]);
    }

    [Fact]
    public void FactBag_NullValue_CanBeStored()
    {
        FactBag bag = new();
        bag["nullKey"] = null;

        Assert.Null(bag["nullKey"]);
    }

    [Theory]
    [InlineData("key1")]
    [InlineData("key2")]
    [InlineData("key3")]
    public void FactBag_UpdateExistingKey_OverwritesValue(string key)
    {
        FactBag bag = new();
        bag[key] = "original";
        bag[key] = "updated";

        Assert.Equal("updated", bag[key]);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(10)]
    public void FactBag_MultipleKeys_AllValuesAccessible(int keyCount)
    {
        FactBag bag = new();
        for (int i = 0; i < keyCount; i++) bag[$"key{i}"] = $"value{i}";

        for (int i = 0; i < keyCount; i++) Assert.Equal($"value{i}", bag[$"key{i}"]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("test")]
    public void FactBag_EmptyOrWhitespaceStringValue_CanBeStored(string value)
    {
        FactBag bag = new();
        bag["stringKey"] = value;

        Assert.Equal(value, bag["stringKey"]);
    }
}