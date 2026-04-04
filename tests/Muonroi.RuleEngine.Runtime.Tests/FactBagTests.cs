using System.Text.Json;
using FluentAssertions;
using Muonroi.RuleEngine.Abstractions;
using Xunit;

namespace Muonroi.RuleEngine.Runtime.Tests;

public sealed class FactBagTests
{
    [Fact]
    public void Set_And_Get_String()
    {
        FactBag bag = new();
        bag.Set("key", "value");

        bag.Get<string>("key").Should().Be("value");
    }

    [Fact]
    public void Set_And_Get_Int()
    {
        FactBag bag = new();
        bag.Set("num", 42);

        bag.Get<int>("num").Should().Be(42);
    }

    [Fact]
    public void Set_And_Get_Bool()
    {
        FactBag bag = new();
        bag.Set("flag", true);

        bag.Get<bool>("flag").Should().BeTrue();
    }

    [Fact]
    public void Get_NonExistentKey_ReturnsDefault()
    {
        FactBag bag = new();

        bag.Get<string>("missing").Should().BeNull();
        bag.Get<int>("missing").Should().Be(0);
        bag.Get<bool>("missing").Should().BeFalse();
    }

    [Fact]
    public void Get_WrongType_ReturnsDefault()
    {
        FactBag bag = new();
        bag.Set("key", "not-an-int");

        bag.Get<int>("key").Should().Be(0);
    }

    [Fact]
    public void Set_OverwritesExistingValue()
    {
        FactBag bag = new();
        bag.Set("key", "first");
        bag.Set("key", "second");

        bag.Get<string>("key").Should().Be("second");
    }

    [Fact]
    public void Indexer_Set_And_Get()
    {
        FactBag bag = new();
        bag["key"] = "value";

        bag["key"].Should().Be("value");
    }

    [Fact]
    public void Indexer_NonExistentKey_ReturnsNull()
    {
        FactBag bag = new();

        bag["missing"].Should().BeNull();
    }

    [Fact]
    public void TryGet_ExistingKey_ReturnsTrueAndValue()
    {
        FactBag bag = new();
        bag.Set("key", "value");

        bool found = bag.TryGet("key", out string? value);

        found.Should().BeTrue();
        value.Should().Be("value");
    }

    [Fact]
    public void TryGet_NonExistentKey_ReturnsFalse()
    {
        FactBag bag = new();

        bool found = bag.TryGet("missing", out string? value);

        found.Should().BeFalse();
        value.Should().BeNull();
    }

    [Fact]
    public void TryGet_WrongType_ReturnsFalse()
    {
        FactBag bag = new();
        bag.Set("key", 42);

        bool found = bag.TryGet("key", out string? value);

        found.Should().BeFalse();
        value.Should().BeNull();
    }

    [Fact]
    public void Remove_ExistingKey_ReturnsTrue()
    {
        FactBag bag = new();
        bag.Set("key", "value");

        bag.Remove("key").Should().BeTrue();
        bag.Get<string>("key").Should().BeNull();
    }

    [Fact]
    public void Remove_NonExistentKey_ReturnsFalse()
    {
        FactBag bag = new();

        bag.Remove("missing").Should().BeFalse();
    }

    [Fact]
    public void AsReadOnly_ReturnsAllFacts()
    {
        FactBag bag = new();
        bag.Set("a", 1);
        bag.Set("b", "two");

        IReadOnlyDictionary<string, object?> facts = bag.AsReadOnly();

        facts.Should().HaveCount(2);
        facts["a"].Should().Be(1);
        facts["b"].Should().Be("two");
    }

    [Fact]
    public void Keys_ReturnsAllKeys()
    {
        FactBag bag = new();
        bag.Set("x", 1);
        bag.Set("y", 2);

        bag.Keys.Should().BeEquivalentTo(["x", "y"]);
    }

    [Fact]
    public void Get_NullValue_ReturnsDefault()
    {
        FactBag bag = new();
        bag.Set<object?>("key", null);

        bag.Get<string>("key").Should().BeNull();
    }

    [Fact]
    public void Get_JsonElementNumber_ConvertedToInt()
    {
        FactBag bag = new();
        JsonElement element = JsonDocument.Parse("42").RootElement;
        bag["num"] = element;

        bag.Get<int>("num").Should().Be(42);
    }

    [Fact]
    public void Get_JsonElementNumber_ConvertedToDouble()
    {
        FactBag bag = new();
        JsonElement element = JsonDocument.Parse("3.14").RootElement;
        bag["num"] = element;

        bag.Get<double>("num").Should().BeApproximately(3.14, 0.001);
    }

    [Fact]
    public void Get_JsonElementString_ConvertedToString()
    {
        FactBag bag = new();
        JsonElement element = JsonDocument.Parse("\"hello\"").RootElement;
        bag["str"] = element;

        bag.Get<string>("str").Should().Be("hello");
    }

    [Fact]
    public void Get_JsonElementBool_ConvertedToBool()
    {
        FactBag bag = new();
        bag["t"] = JsonDocument.Parse("true").RootElement;
        bag["f"] = JsonDocument.Parse("false").RootElement;

        bag.Get<bool>("t").Should().BeTrue();
        bag.Get<bool>("f").Should().BeFalse();
    }

    [Fact]
    public void Get_JsonElementLong_ConvertedToLong()
    {
        FactBag bag = new();
        JsonElement element = JsonDocument.Parse("9999999999").RootElement;
        bag["num"] = element;

        bag.Get<long>("num").Should().Be(9999999999L);
    }

    [Fact]
    public void Get_JsonElementDecimal_ConvertedToDecimal()
    {
        FactBag bag = new();
        JsonElement element = JsonDocument.Parse("123.456").RootElement;
        bag["num"] = element;

        bag.Get<decimal>("num").Should().Be(123.456m);
    }

    [Fact]
    public void Get_ComplexObject_ReturnsCorrectly()
    {
        FactBag bag = new();
        Dictionary<string, object?> dict = new() { ["key"] = "value" };
        bag.Set("dict", dict);

        bag.Get<Dictionary<string, object?>>("dict").Should().BeSameAs(dict);
    }

    [Fact]
    public void Get_NullableType_ReturnsNull()
    {
        FactBag bag = new();

        bag.Get<bool?>("missing").Should().BeNull();
        bag.Get<int?>("missing").Should().BeNull();
    }
}
