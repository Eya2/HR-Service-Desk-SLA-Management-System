using HrServiceDesk.Application.Common.Results;

namespace HrServiceDesk.Application.Tests.Common;

public class ResultTests
{
    private static readonly Error NotFound = Error.NotFound("ticket.not_found", "Ticket not found.");

    [Fact]
    public void Success_exposes_value()
    {
        Result<int> result = 42;

        result.IsSuccess.Should().BeTrue();
        result.Error.Should().BeNull();
        result.Value.Should().Be(42);
    }

    [Fact]
    public void Failure_exposes_error_and_hides_value()
    {
        Result<int> result = NotFound;

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(NotFound);
        var read = () => result.Value;
        read.Should().Throw<InvalidOperationException>().WithMessage("*ticket.not_found*");
    }

    [Fact]
    public void Non_generic_result_converts_from_error()
    {
        Result result = NotFound;

        result.IsSuccess.Should().BeFalse();
        result.Error!.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public void Failure_requires_an_error()
    {
        var act = () => Result.Failure(null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
