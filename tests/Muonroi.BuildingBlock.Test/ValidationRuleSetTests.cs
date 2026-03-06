namespace Muonroi.BuildingBlock.Test;

public class ValidationRuleSetTests
{
    private sealed class Model
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    private sealed class ModelValidator : AbstractValidator<Model>
    {
        public ModelValidator()
        {
            RuleSet("Create", () =>
            {
                RuleFor(x => x.Name).NotEmpty();
            });

            RuleSet("Update", () =>
            {
                RuleFor(x => x.Id).GreaterThan(0);
            });
        }
    }

    [Fact]
    public void CreateRuleset_ValidatesNameOnly()
    {
        ModelValidator validator = new();
        Model model = new()
        {
            Name = ""
        };
        FluentValidation.Results.ValidationResult result = validator.Validate(model, options => options.IncludeRuleSets("Create"));
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(Model.Name));
    }

    [Fact]
    public void UpdateRuleset_ValidatesIdOnly()
    {
        ModelValidator validator = new();
        Model model = new()
        {
            Id = 0,
            Name = "ok"
        };
        FluentValidation.Results.ValidationResult result = validator.Validate(model, options => options.IncludeRuleSets("Update"));
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(Model.Id));
    }
}
