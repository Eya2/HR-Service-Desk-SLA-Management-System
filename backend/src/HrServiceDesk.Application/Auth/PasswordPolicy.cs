using FluentValidation;

namespace HrServiceDesk.Application.Auth;

/// <summary>Password rules: 12 to 128 characters with upper case, lower case, a digit and a symbol.</summary>
public static class PasswordPolicy
{
    public const int MinLength = 12;
    public const int MaxLength = 128;

    public static IRuleBuilderOptions<T, string> MustSatisfyPasswordPolicy<T>(this IRuleBuilder<T, string> rule) =>
        rule
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(MinLength).WithMessage($"Password must be at least {MinLength} characters.")
            .MaximumLength(MaxLength).WithMessage($"Password must be at most {MaxLength} characters.")
            .Must(p => p.Any(char.IsUpper)).WithMessage("Password must contain an upper-case letter.")
            .Must(p => p.Any(char.IsLower)).WithMessage("Password must contain a lower-case letter.")
            .Must(p => p.Any(char.IsDigit)).WithMessage("Password must contain a digit.")
            .Must(p => p.Any(c => !char.IsLetterOrDigit(c))).WithMessage("Password must contain a symbol.");
}
