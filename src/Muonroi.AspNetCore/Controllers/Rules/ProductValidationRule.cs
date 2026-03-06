namespace Muonroi.AspNetCore.Controllers.Rules;

/// <summary>
/// Example business rule for validating Product entities in Auto CRUD operations.
/// This demonstrates how to apply business logic to Auto CRUD without writing manual code.
/// </summary>
/// <typeparam name="TProduct">Product entity type that extends MEntity.</typeparam>
public class ProductValidationRule<TProduct> : IRule<CrudContext<TProduct>>
    where TProduct : MEntity
{
    public string Code => "PRODUCT_VALIDATION";

    public string Name => "Product Validation Rule";

    public int Order => 10;

    public IReadOnlyList<string> DependsOn => [];

    public IEnumerable<Type> Dependencies => [];

    public HookPoint HookPoint => HookPoint.BeforeCreate;

    public RuleType Type => RuleType.Validation;

    public async Task<RuleResult> EvaluateAsync(CrudContext<TProduct> context, FactBag facts, CancellationToken cancellationToken)
    {
        List<string> errors = [];

        PropertyInfo? priceProperty = typeof(TProduct).GetProperty("Price");
        if (priceProperty != null)
        {
            object? price = priceProperty.GetValue(context.Entity);
            if (price is decimal priceValue && priceValue <= 0)
            {
                errors.Add("Price must be greater than 0");
                context.ValidationErrors.Add("Price must be greater than 0");
            }
        }

        PropertyInfo? stockProperty = typeof(TProduct).GetProperty("Stock");
        if (stockProperty != null)
        {
            object? stock = stockProperty.GetValue(context.Entity);
            if (stock is int stockValue && stockValue < 0)
            {
                errors.Add("Stock cannot be negative");
                context.ValidationErrors.Add("Stock cannot be negative");
            }
        }

        PropertyInfo? nameProperty = typeof(TProduct).GetProperty("Name");
        if (nameProperty != null)
        {
            object? name = nameProperty.GetValue(context.Entity);
            if (name is string nameValue && string.IsNullOrWhiteSpace(nameValue))
            {
                errors.Add("Product name is required");
                context.ValidationErrors.Add("Product name is required");
            }
        }

        if (errors.Count > 0)
        {
            return RuleResult.Failure([.. errors]);
        }

        return await Task.FromResult(RuleResult.Passed());
    }

    public Task ExecuteAsync(CrudContext<TProduct> context, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
