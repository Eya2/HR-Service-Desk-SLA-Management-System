using HrServiceDesk.Application.Common.Results;
using HrServiceDesk.Domain.Tickets;

namespace HrServiceDesk.Application.Tickets;

public static class TicketErrors
{
    public static readonly Error NotFound = Error.NotFound("ticket.not_found", "Case not found.");
    public static readonly Error AttachmentNotFound = Error.NotFound("attachment.not_found", "Attachment not found.");
    public static readonly Error RequestTypeNotFound = Error.NotFound("request_type.not_found", "Request type not found.");
    public static readonly Error ReadOnly = Error.Forbidden("ticket.read_only", "You can view this case but not change it.");
    public static readonly Error InternalCommentForbidden = Error.Forbidden("comment.internal_forbidden", "Only HR staff can write internal notes.");
    public static readonly Error PriorityForbidden = Error.Forbidden("ticket.priority_forbidden", "Only HR staff can change the priority.");
    public static Error TransitionNotPermitted(TicketStatus from, TicketStatus to) =>
        Error.Forbidden("ticket.transition_not_permitted", $"You are not allowed to move this case from {from} to {to}.");

    public static Error InvalidTransition(TicketStatus from, TicketStatus to) =>
        Error.DomainRule("ticket.invalid_transition", $"A case cannot move from {from} to {to}.");

    public static readonly Error AlreadyAssigned = Error.Conflict("ticket.already_assigned", "Someone else is already working on this case.");
    public static readonly Error StaffOnly = Error.Forbidden("ticket.staff_only", "Only HR staff can assign cases.");
    public static readonly Error AssigneeNotStaff = Error.Validation("ticket.invalid_assignee", "Cases can only be assigned to active HR staff of this organisation.");
    public static readonly Error TeamNotFound = Error.NotFound("team.not_found", "Team not found.");

    public static readonly Error NotEditable = Error.DomainRule("ticket.not_editable", "This case can no longer be edited.");
}
