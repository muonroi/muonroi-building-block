namespace Quickstart.Mediator.Api.Validators;

/// <summary>
/// FluentValidation validator for <see cref="CreateOrderCommand"/>.
/// The built-in <c>ValidationBehavior&lt;TRequest, TResponse&gt;</c> (registered via
/// <c>AddMuonroiEcosystem()</c>) automatically invokes this validator before the handler runs
/// and throws <c>MValidationException</c> on failure — no manual wiring required.
/// </summary>
public sealed class CreateOrderCommandValidator : AbstractValidator<CreateOrderCommand>
{
    public CreateOrderCommandValidator()
    {
        RuleFor(x => x.ProductName)
            .NotEmpty().WithMessage("Product name is required.")
            .MaximumLength(200).WithMessage("Product name must not exceed 200 characters.");

        RuleFor(x => x.Quantity)
            .GreaterThan(0).WithMessage("Quantity must be greater than zero.");

        RuleFor(x => x.UnitPrice)
            .GreaterThan(0m).WithMessage("Unit price must be greater than zero.");
    }
}
