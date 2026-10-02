using System.Text.Json.Nodes;
using HrServiceDesk.Application.Abstractions;
using HrServiceDesk.Application.Common.Results;
using HrServiceDesk.Domain.Catalog;
using HrServiceDesk.Domain.Tickets;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HrServiceDesk.Application.Tickets.Queries;

public sealed record GetTicketQuery(Guid Id) : IRequest<Result<TicketDetailsDto>>;

internal sealed class GetTicketHandler(IAppDbContext db, ICurrentUser currentUser)
    : IRequestHandler<GetTicketQuery, Result<TicketDetailsDto>>
{
    public async Task<Result<TicketDetailsDto>> Handle(GetTicketQuery request, CancellationToken cancellationToken)
    {
        var ticket = await db.Tickets.AsNoTracking()
            .VisibleTo(currentUser)
            .Include(t => t.Comments)
            .Include(t => t.Attachments)
            .SingleOrDefaultAsync(t => t.Id == request.Id, cancellationToken);
        if (ticket is null)
            return TicketErrors.NotFound;

        var type = await db.RequestTypes.AsNoTracking().SingleAsync(t => t.Id == ticket.RequestTypeId, cancellationToken);
        var seesInternal = TicketAccess.SeesAllCases(currentUser);
        var comments = ticket.Comments.Where(c => seesInternal || !c.IsInternal).OrderBy(c => c.CreatedAt).ToList();

        var personIds = new HashSet<Guid> { ticket.RequesterId };
        if (ticket.AssigneeId is { } assigneeId)
            personIds.Add(assigneeId);
        personIds.UnionWith(comments.Select(c => c.AuthorId));
        personIds.UnionWith(ticket.Attachments.Select(a => a.UploadedById));
        var people = await db.Users.AsNoTracking()
            .Where(u => personIds.Contains(u.Id))
            .Select(u => new PersonDto(u.Id, u.FirstName + " " + u.LastName, u.Email))
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        string NameOf(Guid id) => people.TryGetValue(id, out var p) ? p.FullName : "Unknown user";

        var attachments = ticket.Attachments.OrderBy(a => a.CreatedAt)
            .Select(a => new AttachmentDto(a.Id, a.FileName, a.ContentType, a.SizeBytes, a.FieldKey, NameOf(a.UploadedById), a.CreatedAt))
            .ToList();

        return new TicketDetailsDto(
            ticket.Id,
            ticket.Reference,
            ticket.Title,
            ticket.Description,
            type.Id,
            type.Name,
            type.Category.ToString(),
            ticket.Status.ToString(),
            ticket.Priority.ToString(),
            ticket.IsConfidential,
            people[ticket.RequesterId],
            ticket.AssigneeId is { } a2 && people.TryGetValue(a2, out var assignee) ? assignee : null,
            BuildAnswers(type.Schema, ticket.FormData, attachments),
            attachments,
            comments.Select(c => new CommentDto(c.Id, c.AuthorId, NameOf(c.AuthorId), c.Body, c.IsInternal, c.CreatedAt)).ToList(),
            Permissions(ticket),
            ticket.CreatedAt,
            ticket.UpdatedAt);
    }

    private TicketPermissionsDto Permissions(Ticket ticket)
    {
        var participates = TicketAccess.CanParticipate(ticket, currentUser) && !ticket.IsFinal;
        var isStaff = TicketAccess.IsStaff(currentUser);
        var isRequester = ticket.RequesterId == currentUser.UserId;
        return new TicketPermissionsDto(
            CanComment: participates,
            CanCommentInternally: participates && isStaff,
            CanEdit: participates && (isStaff || (isRequester && ticket.Status == TicketStatus.New)),
            CanChangePriority: participates && isStaff,
            CanAttach: participates);
    }

    /// <summary>Labels the stored answers with the current form definition; answers to removed fields are kept under their key.</summary>
    private static List<FormAnswerDto> BuildAnswers(FormSchema schema, string formData, List<AttachmentDto> attachments)
    {
        var values = JsonNode.Parse(formData) as JsonObject ?? [];
        var answers = new List<FormAnswerDto>();

        foreach (var field in schema.Fields)
        {
            if (field.Type == FormFieldType.File)
            {
                var files = attachments.Where(a => a.FieldKey == field.Key).ToList();
                if (files.Count > 0)
                    answers.Add(new FormAnswerDto(field.Key, field.Label, field.Type, null, files));
            }
            else if (values[field.Key] is { } value)
            {
                answers.Add(new FormAnswerDto(field.Key, field.Label, field.Type, value.DeepClone(), []));
            }
        }

        foreach (var (key, value) in values.Where(kv => schema.Field(kv.Key) is null))
            answers.Add(new FormAnswerDto(key, key, FormFieldType.Text, value?.DeepClone(), []));

        return answers;
    }
}
