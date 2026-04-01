using FluentValidation;
using Muonroi.Core.Abstractions.Exceptions;
using Muonroi.Mediator.Mediator.Interfaces;

namespace Muonroi.Mediator.Behaviours;

/// <summary>
/// Represents the Validation Behavior.
/// </summary>
public class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    /// <summary>
    /// Executes the Handle operation.
    /// </summary>
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!validators.Any()) return await next();

        ValidationContext<TRequest> context = new(request);
        var validationResults =
            await Task.WhenAll(validators.Select(v => v.ValidateAsync(context, cancellationToken)));
        var failures = validationResults.SelectMany(r => r.Errors).Where(f => f != null).ToList();

        if (failures.Count != 0)
        {
            var errors = failures.Select(f => new MValidationError(f.PropertyName, f.ErrorMessage, f.AttemptedValue));
            throw new MValidationException(errors);
        }

        return await next();
    }
}
