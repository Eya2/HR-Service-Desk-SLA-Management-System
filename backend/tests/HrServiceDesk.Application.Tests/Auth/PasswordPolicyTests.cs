using FluentValidation;
using HrServiceDesk.Application.Auth;

namespace HrServiceDesk.Application.Tests.Auth;

public class PasswordPolicyTests
{
    private sealed record Input(string Password);

    private sealed class InputValidator : AbstractValidator<Input>
    {
        public InputValidator() => RuleFor(i => i.Password).MustSatisfyPasswordPolicy();
    }

    private static readonly InputValidator Validator = new();

    [Theory]
    [InlineData("Correct-Horse-9")]
    [InlineData("Ab1!Ab1!Ab1!")]
    public void Accepts_strong_passwords(string password) =>
        Validator.Validate(new Input(password)).IsValid.Should().BeTrue();

    [Theory]
    [InlineData("", "required")]
    [InlineData("Ab1!Ab1!Ab1", "at least 12")]
    [InlineData("correct-horse-9", "upper-case")]
    [InlineData("CORRECT-HORSE-9", "lower-case")]
    [InlineData("Correct-Horse-X", "digit")]
    [InlineData("CorrectHorse99", "symbol")]
    public void Rejects_weak_passwords(string password, string expectedMessage)
    {
        var result = Validator.Validate(new Input(password));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains(expectedMessage, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Rejects_passwords_over_the_maximum_length() =>
        Validator.Validate(new Input("Aa1!" + new string('x', PasswordPolicy.MaxLength))).IsValid.Should().BeFalse();
}
