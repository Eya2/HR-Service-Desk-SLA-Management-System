using System.Text.Json;
using HrServiceDesk.Domain.Catalog;
using HrServiceDesk.Domain.Common;
using HrServiceDesk.Domain.Users;
using HrServiceDesk.Domain.Workflows;

namespace HrServiceDesk.Domain.Tickets;

/// <summary>
/// An HR case. Status, comments and attachments only change through the aggregate, and every change
/// appends a <see cref="TicketEvent"/> to the audit trail.
/// </summary>
public sealed class Ticket : Entity, ITenantOwned, IAuditable
{
    public const int TitleMaxLength = 200;
    public const int DescriptionMaxLength = 4000;

    private readonly List<Comment> _comments = [];
    private readonly List<Attachment> _attachments = [];
    private readonly List<TicketEvent> _events = [];
    private readonly List<TicketApproval> _approvals = [];

    // Last timestamp handed out by this instance (see Record).
    private DateTimeOffset _lastEventAt = DateTimeOffset.MinValue;

    private Ticket() { }

    public Guid TenantId { get; set; }

    public string Reference { get; private set; } = string.Empty;

    public string Title { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public Guid RequestTypeId { get; private set; }

    public TicketPriority Priority { get; private set; }

    public TicketStatus Status { get; private set; } = TicketStatus.New;

    public Guid RequesterId { get; private set; }

    public Guid? AssigneeId { get; private set; }

    /// <summary>Copied from the request type at submission, so later catalog edits do not expose existing cases.</summary>
    public bool IsConfidential { get; private set; }

    /// <summary>The validated answers to the request type's form (jsonb). File fields hold attachment ids.</summary>
    public string FormData { get; private set; } = "{}";

    /// <summary>Closed, cancelled or rejected cases accept no further changes.</summary>
    public bool IsFinal => TicketStatusMachine.IsTerminal(Status);

    public IReadOnlyCollection<Comment> Comments => _comments.AsReadOnly();

    public IReadOnlyCollection<Attachment> Attachments => _attachments.AsReadOnly();

    public IReadOnlyCollection<TicketEvent> Events => _events.AsReadOnly();

    public IReadOnlyCollection<TicketApproval> Approvals => _approvals.AsReadOnly();

    /// <summary>The step waiting for a decision, if the case is pending approval.</summary>
    public TicketApproval? CurrentApproval =>
        Status == TicketStatus.PendingApproval
            ? _approvals.Where(a => a.Decision == ApprovalDecision.Pending).MinBy(a => a.StepOrder)
            : null;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public static Ticket Submit(
        string reference, RequestType requestType, Guid requesterId, string title, string? description, string formData, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(requestType);
        if (!requestType.IsActive)
            throw new DomainException("ticket.request_type_inactive", "This request type is no longer available.");

        var ticket = new Ticket
        {
            Reference = reference,
            RequestTypeId = requestType.Id,
            RequesterId = requesterId,
            Priority = requestType.DefaultPriority,
            IsConfidential = requestType.IsConfidential,
            FormData = formData,
        };
        ticket.ApplyDetails(title, description);
        ticket.Record(TicketEventType.Created, requesterId, now, new { reference });
        return ticket;
    }

    public void UpdateDetails(string title, string? description, Guid actorId, DateTimeOffset now)
    {
        var before = (Title, Description);
        ApplyDetails(title, description);
        if (before != (Title, Description))
            Record(TicketEventType.DetailsUpdated, actorId, now, new { title = Title });
    }

    public void ChangePriority(TicketPriority priority, Guid actorId, DateTimeOffset now)
    {
        if (priority == Priority)
            return;
        Record(TicketEventType.PriorityChanged, actorId, now, new { from = Priority.ToString(), to = priority.ToString() });
        Priority = priority;
    }

    /// <summary>
    /// Moves the case to <paramref name="to"/> if the status machine allows it for <paramref name="actor"/>.
    /// <paramref name="actorId"/> is null for system-initiated changes.
    /// </summary>
    public void ChangeStatus(TicketStatus to, TransitionActor actor, Guid? actorId, DateTimeOffset now, string? reason = null)
    {
        switch (TicketStatusMachine.Check(Status, to, actor))
        {
            case TransitionCheck.Invalid:
                throw new DomainException("ticket.invalid_transition", $"A case cannot move from {Status} to {to}.");
            case TransitionCheck.NotPermitted:
                throw new DomainException("ticket.transition_not_permitted", $"You are not allowed to move this case from {Status} to {to}.");
        }

        var data = string.IsNullOrWhiteSpace(reason)
            ? (object)new { from = Status.ToString(), to = to.ToString() }
            : new { from = Status.ToString(), to = to.ToString(), reason = reason.Trim() };
        Record(TicketEventType.StatusChanged, actorId, now, data);
        Status = to;

        // Steps still waiting are moot once the case is rejected or withdrawn.
        if (to is TicketStatus.Rejected or TicketStatus.Cancelled)
        {
            foreach (var pending in _approvals.Where(a => a.Decision == ApprovalDecision.Pending))
                pending.Decide(ApprovalDecision.Skipped, null, null, now);
        }
    }

    /// <summary>
    /// Copies the workflow's steps onto the case and sends it for approval. A "Manager" step is assigned to
    /// <paramref name="requesterManagerId"/>; without a manager it falls back to HR Admins, so approval is never skipped.
    /// </summary>
    public void StartApproval(WorkflowDefinition workflow, Guid? requesterManagerId, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        if (_approvals.Count > 0)
            throw new DomainException("ticket.approval_already_started", "Approval has already started for this case.");

        foreach (var step in workflow.OrderedSteps)
        {
            var approval = step.ApproverRole == Role.Manager
                ? requesterManagerId is { } managerId
                    ? new TicketApproval(Id, step.Order, step.Name, Role.Manager, managerId)
                    : new TicketApproval(Id, step.Order, $"{step.Name} (no manager: HR Admin)", Role.HrAdmin, null)
                : new TicketApproval(Id, step.Order, step.Name, step.ApproverRole, null);
            _approvals.Add(approval);
        }

        ChangeStatus(TicketStatus.PendingApproval, TransitionActor.System, null, now);
        RecordApprovalRequested(now);
    }

    /// <summary>
    /// Records the decision on the current step. A rejection rejects the case; approving the last step opens it.
    /// The requester can never approve their own case.
    /// </summary>
    public void DecideApproval(Guid approvalId, bool approve, Guid deciderId, IReadOnlyCollection<Role> deciderRoles, string? comment, DateTimeOffset now)
    {
        var current = CurrentApproval;
        if (current is null || current.Id != approvalId)
            throw new DomainException("approval.not_current", "This approval step is not waiting for a decision.");
        if (deciderId == RequesterId || !current.IsApprover(deciderId, deciderRoles))
            throw new DomainException("approval.not_approver", "You are not an approver for this step.");
        if (!approve && string.IsNullOrWhiteSpace(comment))
            throw new DomainException("approval.reason_required", "A reason is required to reject a request.");
        if (comment?.Trim().Length > TicketApproval.CommentMaxLength)
            throw new DomainException("approval.comment_too_long", $"The comment must be at most {TicketApproval.CommentMaxLength} characters.");

        current.Decide(approve ? ApprovalDecision.Approved : ApprovalDecision.Rejected, deciderId, comment, now);
        Record(TicketEventType.ApprovalDecided, deciderId, now, new
        {
            step = current.StepName,
            decision = current.Decision.ToString(),
            comment = current.Comment,
        });

        if (!approve)
            ChangeStatus(TicketStatus.Rejected, TransitionActor.System, deciderId, now, comment);
        else if (CurrentApproval is null)
            ChangeStatus(TicketStatus.Open, TransitionActor.System, deciderId, now);
        else
            RecordApprovalRequested(now);
    }

    private void RecordApprovalRequested(DateTimeOffset now)
    {
        var next = CurrentApproval!;
        Record(TicketEventType.ApprovalRequested, null, now, new { step = next.StepName, approverRole = next.ApproverRole.ToString() });
    }

    /// <summary>Appends an entry to the audit trail.</summary>
    public TicketEvent Record(TicketEventType type, Guid? actorId, DateTimeOffset now, object data, bool isInternal = false)
    {
        // One operation often records several events at the same instant (a reply and a status change).
        // Each gets a strictly later timestamp (1 µs, PostgreSQL's precision) so the trail sorts in the order things happened.
        var at = now > _lastEventAt ? now : _lastEventAt.AddTicks(10);
        _lastEventAt = at;
        var entry = new TicketEvent(Id, type, actorId, at, JsonSerializer.Serialize(data, JsonDefaults.Web), isInternal);
        _events.Add(entry);
        return entry;
    }

    private void ApplyDetails(string title, string? description)
    {
        title = (title ?? string.Empty).Trim();
        description = (description ?? string.Empty).Trim();
        if (title.Length is 0 or > TitleMaxLength)
            throw new DomainException("ticket.invalid_title", $"Title must be 1 to {TitleMaxLength} characters.");
        if (description.Length > DescriptionMaxLength)
            throw new DomainException("ticket.invalid_description", $"Description must be at most {DescriptionMaxLength} characters.");
        Title = title;
        Description = description;
    }

    public void SetFormData(string formData) => FormData = formData;

    public Comment AddComment(Guid authorId, string body, bool isInternal, DateTimeOffset now)
    {
        body = (body ?? string.Empty).Trim();
        if (body.Length is 0 or > Comment.BodyMaxLength)
            throw new DomainException("comment.invalid_body", $"A comment must be 1 to {Comment.BodyMaxLength} characters.");

        var comment = new Comment(Id, authorId, body, isInternal, now);
        _comments.Add(comment);
        Record(TicketEventType.CommentAdded, authorId, now, new { commentId = comment.Id, isInternal }, isInternal);
        return comment;
    }

    public Attachment AddAttachment(
        string fileName, string contentType, long sizeBytes, string storageKey, Guid uploadedById, string? fieldKey, DateTimeOffset now)
    {
        var attachment = new Attachment(Id, fileName, contentType, sizeBytes, storageKey, uploadedById, fieldKey, now);
        _attachments.Add(attachment);
        // Files answering a form field come with the submission and are part of the "Created" event.
        if (fieldKey is null)
            Record(TicketEventType.AttachmentAdded, uploadedById, now, new { fileName });
        return attachment;
    }
}
