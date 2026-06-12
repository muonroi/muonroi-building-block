using System.Collections.Generic;
using Xunit;

namespace Muonroi.RuleEngine.Abstractions.Tests;

public class FactBagTests
{
    [Fact]
    public void Indexer_gets_null_for_missing_key()
    {
        // Arrange
        var bag = new FactBag();

        // Act
        var value = bag["missing"];

        // Assert
        Assert.Null(value);
    }

    [Fact]
    public void Indexer_set_then_get_returns_value()
    {
        // Arrange
        var bag = new FactBag();

        // Act
        bag["answer"] = 42;
        var value = bag["answer"];

        // Assert
        Assert.Equal(42, value);
    }

    [Fact]
    public void Indexer_overwrites_existing_value()
    {
        // Arrange
        var bag = new FactBag();
        bag["key"] = "first";

        // Act
        bag["key"] = "second";

        // Assert
        Assert.Equal("second", bag["key"]);
    }

    [Fact]
    public void Set_adds_new_fact()
    {
        // Arrange
        var bag = new FactBag();

        // Act
        bag.Set("id", 7);

        // Assert
        Assert.Equal(7, bag.Get<int>("id"));
    }

    [Fact]
    public void Set_overwrites_existing_fact()
    {
        // Arrange
        var bag = new FactBag();
        bag.Set("id", 1);

        // Act
        bag.Set("id", 2);

        // Assert
        Assert.Equal(2, bag.Get<int>("id"));
    }

    [Fact]
    public void Get_returns_value_for_matching_type()
    {
        // Arrange
        var bag = new FactBag();
        bag.Set("name", "muonroi");

        // Act
        var value = bag.Get<string>("name");

        // Assert
        Assert.Equal("muonroi", value);
    }

    [Fact]
    public void Get_returns_default_for_missing_key()
    {
        // Arrange
        var bag = new FactBag();

        // Act
        var value = bag.Get<int>("missing");

        // Assert
        Assert.Equal(0, value);
    }

    [Fact]
    public void Get_returns_default_for_wrong_type()
    {
        // Arrange
        var bag = new FactBag();
        bag.Set("id", 10);

        // Act
        var value = bag.Get<string>("id");

        // Assert
        Assert.Null(value);
    }

    [Fact]
    public void TryGet_returns_true_and_value_for_matching_type()
    {
        // Arrange
        var bag = new FactBag();
        bag.Set("id", 10);

        // Act
        var success = bag.TryGet<int>("id", out var value);

        // Assert
        Assert.True(success);
        Assert.Equal(10, value);
    }

    [Fact]
    public void TryGet_returns_false_for_missing_key()
    {
        // Arrange
        var bag = new FactBag();

        // Act
        var success = bag.TryGet<int>("missing", out var value);

        // Assert
        Assert.False(success);
        Assert.Equal(0, value);
    }

    [Fact]
    public void TryGet_returns_false_for_wrong_type()
    {
        // Arrange
        var bag = new FactBag();
        bag.Set("id", 10);

        // Act
        var success = bag.TryGet<string>("id", out var value);

        // Assert
        Assert.False(success);
        Assert.Null(value);
    }

    [Fact]
    public void TryGet_returns_false_when_value_is_null()
    {
        // Arrange
        var bag = new FactBag();
        bag.Set<string?>("name", null);

        // Act
        var success = bag.TryGet<string>("name", out var value);

        // Assert
        Assert.False(success);
        Assert.Null(value);
    }

    [Fact]
    public void Set_allows_null_value_and_indexer_returns_null()
    {
        // Arrange
        var bag = new FactBag();

        // Act
        bag.Set<string?>("name", null);
        var value = bag["name"];

        // Assert
        Assert.Null(value);
    }

    [Fact]
    public void Get_returns_null_for_null_value()
    {
        // Arrange
        var bag = new FactBag();
        bag.Set<string?>("name", null);

        // Act
        var value = bag.Get<string>("name");

        // Assert
        Assert.Null(value);
    }

    [Fact]
    public void AsReadOnly_returns_same_instance()
    {
        // Arrange
        var bag = new FactBag();

        // Act
        var view = bag.AsReadOnly();

        // Assert
        Assert.Same(view, bag.AsReadOnly());
    }

    [Fact]
    public void AsReadOnly_reflects_changes_after_set()
    {
        // Arrange
        var bag = new FactBag();
        var view = bag.AsReadOnly();

        // Act
        bag.Set("flag", true);

        // Assert
        Assert.True((bool)view["flag"]!);
    }

    [Fact]
    public void Remove_existing_returns_true_and_removes()
    {
        // Arrange
        var bag = new FactBag();
        bag.Set("key", "value");

        // Act
        var removed = bag.Remove("key");

        // Assert
        Assert.True(removed);
        Assert.Null(bag["key"]);
    }

    [Fact]
    public void Remove_missing_returns_false()
    {
        // Arrange
        var bag = new FactBag();

        // Act
        var removed = bag.Remove("missing");

        // Assert
        Assert.False(removed);
    }

    [Fact]
    public void Keys_contains_added_keys()
    {
        // Arrange
        var bag = new FactBag();
        bag.Set("a", 1);
        bag.Set("b", 2);

        // Act
        var keys = bag.Keys;

        // Assert
        Assert.Contains("a", keys);
        Assert.Contains("b", keys);
    }

    [Fact]
    public void Keys_empty_when_no_facts()
    {
        // Arrange
        var bag = new FactBag();

        // Act
        var keys = bag.Keys;

        // Assert
        Assert.Empty(keys);
    }

    [Fact]
    public void Empty_string_key_is_allowed()
    {
        // Arrange
        var bag = new FactBag();

        // Act
        bag.Set(string.Empty, "value");

        // Assert
        Assert.Equal("value", bag.Get<string>(string.Empty));
    }

    [Fact]
    public void Null_key_in_indexer_set_throws()
    {
        // Arrange
        var bag = new FactBag();

        // Act
        void Act() => bag[null!] = "value";

        // Assert
        Assert.Throws<ArgumentNullException>(Act);
    }

    [Fact]
    public void Null_key_in_indexer_get_throws()
    {
        // Arrange
        var bag = new FactBag();

        // Act
        object? Act() => bag[null!];

        // Assert
        Assert.Throws<ArgumentNullException>(Act);
    }

    [Fact]
    public void Null_key_in_set_throws()
    {
        // Arrange
        var bag = new FactBag();

        // Act
        void Act() => bag.Set(null!, "value");

        // Assert
        Assert.Throws<ArgumentNullException>(Act);
    }

    [Fact]
    public void Null_key_in_get_throws()
    {
        // Arrange
        var bag = new FactBag();

        // Act
        string? Act() => bag.Get<string>(null!);

        // Assert
        Assert.Throws<ArgumentNullException>(Act);
    }

    [Fact]
    public void Null_key_in_tryget_throws()
    {
        // Arrange
        var bag = new FactBag();

        // Act
        void Act() => bag.TryGet<string>(null!, out _);

        // Assert
        Assert.Throws<ArgumentNullException>(Act);
    }

    [Fact]
    public void Keys_are_case_sensitive()
    {
        // Arrange
        var bag = new FactBag();
        bag.Set("Key", 1);

        // Act
        var value = bag.Get<int>("key");

        // Assert
        Assert.Equal(0, value);
    }

    [Fact]
    public void TryGet_supports_interface_type()
    {
        // Arrange
        var bag = new FactBag();
        bag.Set("list", new List<int> { 1, 2 });

        // Act
        var success = bag.TryGet<IReadOnlyList<int>>("list", out var value);

        // Assert
        Assert.True(success);
        Assert.Equal(2, value!.Count);
    }

    [Fact]
    public void Get_supports_base_type()
    {
        // Arrange
        var bag = new FactBag();
        bag.Set("name", "value");

        // Act
        var value = bag.Get<object>("name");

        // Assert
        Assert.Equal("value", value);
    }

    [Fact]
    public void Keys_update_after_remove()
    {
        // Arrange
        var bag = new FactBag();
        bag.Set("a", 1);

        // Act
        bag.Remove("a");

        // Assert
        Assert.Empty(bag.Keys);
    }

    [Theory]
    [InlineData(123)]
    [InlineData(true)]
    [InlineData("text")]
    public void Indexer_gets_round_trip_values(object value)
    {
        // Arrange
        var bag = new FactBag();

        // Act
        bag["value"] = value;

        // Assert
        Assert.Equal(value, bag["value"]);
    }

    [Theory]
    [InlineData("42", 42)]
    [InlineData("007", 7)]
    public void Get_does_not_convert_types(string stored, int expected)
    {
        // Arrange
        var bag = new FactBag();
        bag.Set("value", stored);

        // Act
        var value = bag.Get<int>("value");

        // Assert
        Assert.Equal(0, value);
        Assert.NotEqual(expected, value);
    }

    [Theory]
    [InlineData("value")]
    [InlineData("")]
    public void TryGet_returns_true_for_matching_string_values(string stored)
    {
        // Arrange
        var bag = new FactBag();
        bag.Set("name", stored);

        // Act
        var success = bag.TryGet<string>("name", out var value);

        // Assert
        Assert.True(success);
        Assert.Equal(stored, value);
    }
}
