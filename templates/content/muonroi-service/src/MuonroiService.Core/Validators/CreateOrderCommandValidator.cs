#if (isAggregate)
using MuonroiService.Core.Commands;

namespace MuonroiService.Core.Validators;

/// <summary>
/// Validates <see cref="CreateOrderCommand"/> before dispatch.
/// Replace with your domain validation logic.
/// </summary>
public static class CreateOrderCommandValidator
{
    public static IEnumerable<string> Validate(CreateOrderCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.Name))
            yield return "Name is required.";
        if (string.IsNullOrWhiteSpace(command.LinerCode))
            yield return "LinerCode is required.";
    }
}
#endif
