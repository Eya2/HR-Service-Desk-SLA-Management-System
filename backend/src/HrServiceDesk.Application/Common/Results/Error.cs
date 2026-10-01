namespace HrServiceDesk.Application.Common.Results;

public enum ErrorType
{
    Validation,
    NotFound,
    Conflict,
    Forbidden,
    Unauthorized,
    DomainRule,
}

/// <summary>An expected failure. <see cref="Code"/> is stable and machine-readable (e.g. <c>ticket.not_found</c>).</summary>
public sealed record Error(string Code, string Message, ErrorType Type)
{
    public static Error NotFound(string code, string message) => new(code, message, ErrorType.NotFound);
    public static Error Conflict(string code, string message) => new(code, message, ErrorType.Conflict);
    public static Error Forbidden(string code, string message) => new(code, message, ErrorType.Forbidden);
    public static Error Unauthorized(string code, string message) => new(code, message, ErrorType.Unauthorized);
    public static Error Validation(string code, string message) => new(code, message, ErrorType.Validation);
    public static Error DomainRule(string code, string message) => new(code, message, ErrorType.DomainRule);
}
