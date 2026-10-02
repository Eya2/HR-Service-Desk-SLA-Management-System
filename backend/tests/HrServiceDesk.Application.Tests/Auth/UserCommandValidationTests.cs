using HrServiceDesk.Application.Users.Commands;

namespace HrServiceDesk.Application.Tests.Auth;

public class UserCommandValidationTests
{
    private static CreateUserCommand Valid() =>
        new("new.user@acme.example", "New", "User", ["Employee"], null, "Correct-Horse-9");

    private static readonly CreateUserValidator Validator = new();

    [Fact]
    public void Valid_command_passes() => Validator.Validate(Valid()).IsValid.Should().BeTrue();

    [Fact]
    public void Unknown_role_is_rejected()
    {
        var result = Validator.Validate(Valid() with { Roles = ["Employee", "Wizard"] });

        result.Errors.Should().ContainSingle(e => e.PropertyName == nameof(CreateUserCommand.Roles));
    }

    [Fact]
    public void Role_names_are_case_sensitive()
    {
        Validator.Validate(Valid() with { Roles = ["employee"] }).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Empty_roles_are_rejected()
    {
        Validator.Validate(Valid() with { Roles = [] }).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Invalid_email_is_rejected()
    {
        Validator.Validate(Valid() with { Email = "nope" }).IsValid.Should().BeFalse();
    }
}
