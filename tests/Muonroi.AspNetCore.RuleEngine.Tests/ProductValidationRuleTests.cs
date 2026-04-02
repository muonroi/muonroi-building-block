namespace Muonroi.AspNetCore.RuleEngine.Tests;

using Muonroi.AspNetCore.Controllers.Rules;

public class ProductValidationRuleTests
{
    public class TestProduct : MEntity
    {
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int Stock { get; set; }
    }

    private readonly ProductValidationRule<TestProduct> _rule = new();

    [Fact]
    public async Task Evaluate_ShouldPass_WhenProductIsValid()
    {
        // Arrange
        var product = new TestProduct { Name = "Laptop", Price = 1000, Stock = 10 };
        var context = new CrudContext<TestProduct> { Entity = product, OperationType = CrudOperationType.Create };
        var facts = new FactBag();

        // Act
        var result = await _rule.EvaluateAsync(context, facts, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Evaluate_ShouldFail_WhenPriceIsZeroOrLess()
    {
        // Arrange
        var product = new TestProduct { Name = "Laptop", Price = 0, Stock = 10 };
        var context = new CrudContext<TestProduct> { Entity = product, OperationType = CrudOperationType.Create };
        var facts = new FactBag();

        // Act
        var result = await _rule.EvaluateAsync(context, facts, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().Contain("Price must be greater than 0");
    }

    [Fact]
    public async Task Evaluate_ShouldFail_WhenStockIsNegative()
    {
        // Arrange
        var product = new TestProduct { Name = "Laptop", Price = 1000, Stock = -1 };
        var context = new CrudContext<TestProduct> { Entity = product, OperationType = CrudOperationType.Create };
        var facts = new FactBag();

        // Act
        var result = await _rule.EvaluateAsync(context, facts, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().Contain("Stock cannot be negative");
    }

    [Fact]
    public async Task Evaluate_ShouldFail_WhenNameIsEmpty()
    {
        // Arrange
        var product = new TestProduct { Name = "", Price = 1000, Stock = 10 };
        var context = new CrudContext<TestProduct> { Entity = product, OperationType = CrudOperationType.Create };
        var facts = new FactBag();

        // Act
        var result = await _rule.EvaluateAsync(context, facts, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().Contain("Product name is required");
    }
}
