namespace Muonroi.Mediator.Tests.Behaviours;

public class ValidationBehaviorTests
{
    private record TestRequest(string Name) : IRequest<string>;

    private class AlwaysPassValidator : AbstractValidator<TestRequest>
    {
        public AlwaysPassValidator() { }
    }

    private class NameRequiredValidator : AbstractValidator<TestRequest>
    {
        public NameRequiredValidator()
        {
            RuleFor(x => x.Name).NotEmpty().WithMessage("Name is required");
        }
    }

    [Fact]
    public async Task Handle_NoValidators_CallsNext()
    {
        var behavior = new ValidationBehavior<TestRequest, string>(
            Array.Empty<IValidator<TestRequest>>());

        string result = await behavior.Handle(
            new TestRequest("test"),
            () => Task.FromResult("OK"),
            CancellationToken.None);

        result.Should().Be("OK");
    }

    [Fact]
    public async Task Handle_ValidRequest_CallsNext()
    {
        var validators = new IValidator<TestRequest>[] { new AlwaysPassValidator() };
        var behavior = new ValidationBehavior<TestRequest, string>(validators);

        string result = await behavior.Handle(
            new TestRequest("test"),
            () => Task.FromResult("OK"),
            CancellationToken.None);

        result.Should().Be("OK");
    }

    [Fact]
    public async Task Handle_InvalidRequest_ThrowsValidationException()
    {
        var validators = new IValidator<TestRequest>[] { new NameRequiredValidator() };
        var behavior = new ValidationBehavior<TestRequest, string>(validators);

        Func<Task> act = () => behavior.Handle(
            new TestRequest(""),
            () => Task.FromResult("OK"),
            CancellationToken.None);

        await act.Should().ThrowAsync<MValidationException>();
    }

    [Fact]
    public async Task Handle_MultipleValidators_AllValidationErrors()
    {
        var validator1 = new NameRequiredValidator();
        var validator2 = new NameRequiredValidator();
        var validators = new IValidator<TestRequest>[] { validator1, validator2 };
        var behavior = new ValidationBehavior<TestRequest, string>(validators);

        Func<Task> act = () => behavior.Handle(
            new TestRequest(""),
            () => Task.FromResult("OK"),
            CancellationToken.None);

        var exception = await act.Should().ThrowAsync<MValidationException>();
        exception.Which.Errors.Should().HaveCountGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task Handle_ValidRequest_MultipleValidators_AllPass()
    {
        var validators = new IValidator<TestRequest>[]
        {
            new AlwaysPassValidator(),
            new NameRequiredValidator()
        };
        var behavior = new ValidationBehavior<TestRequest, string>(validators);

        string result = await behavior.Handle(
            new TestRequest("valid"),
            () => Task.FromResult("OK"),
            CancellationToken.None);

        result.Should().Be("OK");
    }
}
