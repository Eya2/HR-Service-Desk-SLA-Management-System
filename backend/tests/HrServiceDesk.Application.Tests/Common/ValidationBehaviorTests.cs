using FluentValidation;
using HrServiceDesk.Application.Common.Behaviors;

namespace HrServiceDesk.Application.Tests.Common;

public class ValidationBehaviorTests
{
    public sealed record SampleRequest(string Title);

    private sealed class SampleValidator : AbstractValidator<SampleRequest>
    {
        public SampleValidator() => RuleFor(r => r.Title).NotEmpty().MaximumLength(10);
    }

    [Fact]
    public async Task Calls_next_when_request_is_valid()
    {
        var behavior = new ValidationBehavior<SampleRequest, string>([new SampleValidator()]);

        var response = await behavior.Handle(new SampleRequest("ok"), () => Task.FromResult("handled"), CancellationToken.None);

        response.Should().Be("handled");
    }

    [Fact]
    public async Task Throws_with_all_failures_and_does_not_call_next_when_invalid()
    {
        var behavior = new ValidationBehavior<SampleRequest, string>([new SampleValidator()]);
        var nextCalled = false;

        var act = () => behavior.Handle(new SampleRequest(""), () =>
        {
            nextCalled = true;
            return Task.FromResult("handled");
        }, CancellationToken.None);

        (await act.Should().ThrowAsync<ValidationException>())
            .Which.Errors.Should().ContainSingle(e => e.PropertyName == nameof(SampleRequest.Title));
        nextCalled.Should().BeFalse();
    }

    [Fact]
    public async Task Calls_next_when_no_validator_is_registered()
    {
        var behavior = new ValidationBehavior<SampleRequest, string>([]);

        var response = await behavior.Handle(new SampleRequest(""), () => Task.FromResult("handled"), CancellationToken.None);

        response.Should().Be("handled");
    }
}
