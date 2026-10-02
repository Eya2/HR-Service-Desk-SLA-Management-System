using System.Text.Json.Nodes;
using HrServiceDesk.Domain.Catalog;

namespace HrServiceDesk.Application.Tickets;

public sealed record TicketCreatedDto(Guid Id, string Reference);

public sealed record TicketSummaryDto(
    Guid Id,
    string Reference,
    string Title,
    Guid RequestTypeId,
    string RequestTypeName,
    string Category,
    string Status,
    string Priority,
    bool IsConfidential,
    Guid RequesterId,
    string RequesterName,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record PersonDto(Guid Id, string FullName, string Email);

public sealed record AttachmentDto(
    Guid Id, string FileName, string ContentType, long SizeBytes, string? FieldKey, string UploadedByName, DateTimeOffset CreatedAt);

public sealed record CommentDto(Guid Id, Guid AuthorId, string AuthorName, string Body, bool IsInternal, DateTimeOffset CreatedAt);

/// <summary>One answer of the submitted form, labelled with the request type's field definition.</summary>
public sealed record FormAnswerDto(string Key, string Label, FormFieldType Type, JsonNode? Value, IReadOnlyList<AttachmentDto> Files);

/// <summary>What the caller may do on the case, so the UI shows only the actions the API will accept.</summary>
public sealed record TicketPermissionsDto(bool CanComment, bool CanCommentInternally, bool CanEdit, bool CanChangePriority, bool CanAttach);

public sealed record TicketDetailsDto(
    Guid Id,
    string Reference,
    string Title,
    string Description,
    Guid RequestTypeId,
    string RequestTypeName,
    string Category,
    string Status,
    string Priority,
    bool IsConfidential,
    PersonDto Requester,
    PersonDto? Assignee,
    IReadOnlyList<FormAnswerDto> Answers,
    IReadOnlyList<AttachmentDto> Attachments,
    IReadOnlyList<CommentDto> Comments,
    TicketPermissionsDto Permissions,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record AttachmentContent(string FileName, string ContentType, Stream Content);
