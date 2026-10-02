using FluentValidation;
using HrServiceDesk.Domain.Catalog;
using HrServiceDesk.Domain.Tickets;

namespace HrServiceDesk.Application.Catalog;

internal static class CatalogRules
{
    public static readonly Common.Results.Error NotFound = Common.Results.Error.NotFound("request_type.not_found", "Request type not found.");

    public static void ValidName<T>(IRuleBuilder<T, string> rule) => rule.NotEmpty().MaximumLength(RequestType.NameMaxLength);

    public static void ValidDescription<T>(IRuleBuilder<T, string?> rule) => rule.MaximumLength(RequestType.DescriptionMaxLength);

    public static void ValidCategory<T>(IRuleBuilder<T, string> rule) =>
        rule.Must(c => Enum.TryParse<RequestCategory>(c, ignoreCase: false, out _))
            .WithMessage($"Category must be one of: {string.Join(", ", Enum.GetNames<RequestCategory>())}.");

    public static void ValidPriority<T>(IRuleBuilder<T, string> rule) =>
        rule.Must(p => Enum.TryParse<TicketPriority>(p, ignoreCase: false, out _))
            .WithMessage($"Priority must be one of: {string.Join(", ", Enum.GetNames<TicketPriority>())}.");

    public static RequestTypeDto ToDto(RequestType type) => new(
        type.Id,
        type.Name,
        type.Description,
        type.Category.ToString(),
        type.IsConfidential,
        type.IsActive,
        type.DefaultPriority.ToString(),
        type.Schema.Fields);
}
