using System.Text.Json;
using HrServiceDesk.Domain.Catalog;
using HrServiceDesk.Domain.Common;
using HrServiceDesk.Domain.Sla;
using HrServiceDesk.Domain.Users;
using HrServiceDesk.Domain.Workflows;

namespace HrServiceDesk.Domain.Tickets;

/// <summary>
/// An HR case. Status, comments and attachments only change through the aggregate, and every change
/// appends a <see cref="TicketEvent"/> to the audit trail. Children take the case's tenant (a new case gets
/// its tenant when saved, and its children with it), so background jobs need no tenant context.
/// </summary>
public sealed class Ticket : Entity, ITenantOwned, IAuditable
{
    public const int TitleMaxLength = 200;
    public const int DescriptionMaxLength = 4000;

    private readonly List<Comment> _comments = [];
    private readonly List<Attachment> _attachments = [];
    private readonly List<TicketEvent> _events = [];
    private readonly List<TicketApproval> _approvals = [];
    private readonly List<SlaPause> _slaPauses = [];

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

    /// <summary>The team handling the case (from the request type at submission, or after a reassignment).</summary>
    public Guid? TeamId { get; private set; }

    /// <summary>Copied from the request type at submission, so later catalog edits do not expose existing cases.</summary>
    public bool IsConfidential { get; private set; }

    /// <summary>Copied from the request type: views of the case by HR are audited.</summary>
    public bool IsSensitive { get; private set; }

    /// <summary>Set once the retention job has removed the case's personal data.</summary>
    public DateTimeOffset? AnonymizedAt { get; private set; }

    /// <summary>The validated answers to the request type's form (jsonb). File fields hold attachment ids.</summary>
    public string FormData { get; private set; } = "{}";

    /// <summary>Closed, cancelled or rejected cases accept no further changes.</summary>
    public bool IsFinal => TicketStatusMachine.IsTerminal(Status);

    // ---- SLA (targets are copied from the policy when the clock starts) ----

    public Guid? SlaPolicyId { get; private set; }

    public int? FirstResponseTargetMinutes { get; private set; }

    public int? ResolutionTargetMinutes { get; private set; }

    public int SlaAtRiskPercent { get; private set; } = 80;

    public TicketStatus[] SlaPauseStatuses { get; private set; } = [];

    /// <summary>When the clock started (submission); null when no SLA applies.</summary>
    public DateTimeOffset? SlaStartedAt { get; private set; }

    /// <summary>When the clock stopped for good (closed, cancelled or rejected).</summary>
    public DateTimeOffset? SlaStoppedAt { get; private set; }

    public IReadOnlyCollection<SlaPause> SlaPauses => _slaPauses.AsReadOnly();

    public DateTimeOffset? FirstRespondedAt { get; private set; }

    /// <summary>When HR last resolved the case (cleared if it is reopened).</summary>
    public DateTimeOffset? ResolvedAt { get; private set; }

    /// <summary>How many times the employee reopened the case after resolution.</summary>
    public int ReopenCount { get; private set; }

    /// <summary>Computed deadline; null while the clock is paused.</summary>
    public DateTimeOffset? FirstResponseDueAt { get; private set; }

    /// <summary>Computed deadline; null while the clock is paused (including while resolved).</summary>
    public DateTimeOffset? ResolutionDueAt { get; private set; }

    public SlaState SlaState { get; private set; } = SlaState.None;

    public bool FirstResponseBreached { get; private set; }

    public bool ResolutionBreached { get; private set; }

    public bool IsSlaPaused => _slaPauses.Any(p => p.To is null) && SlaStoppedAt is null;

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
            IsSensitive = requestType.IsSensitive || requestType.IsConfidential,
            FormData = formData,
            TeamId = requestType.ResponsibleTeamId,
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

    public void ChangePriority(TicketPriority priority, Guid? actorId, DateTimeOffset now)
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
        var from = Status;
        Status = to;
        if (to == TicketStatus.Resolved)
            ResolvedAt = now;
        else if (to == TicketStatus.Reopened)
        {
            ResolvedAt = null;
            ReopenCount++;
        }

        MoveSlaClock(from, to, now);

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
            approval.TenantId = TenantId;
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

    /// <summary>
    /// Gives the case to <paramref name="assigneeId"/> (null: back to the team queue). <paramref name="actorId"/>
    /// is null for automatic assignment.
    /// </summary>
    public void Assign(Guid? assigneeId, Guid? actorId, DateTimeOffset now)
    {
        if (IsFinal)
            throw new DomainException("ticket.not_assignable", "A closed case cannot be reassigned.");
        if (assigneeId == AssigneeId)
            return;
        Record(TicketEventType.Assigned, actorId, now, new { from = AssigneeId, to = assigneeId, teamId = TeamId });
        AssigneeId = assigneeId;
    }

    /// <summary>An agent takes an unassigned case. Taking a case someone else holds is refused (409).</summary>
    public void Claim(Guid agentId, DateTimeOffset now)
    {
        if (AssigneeId is { } current && current != agentId)
            throw new DomainException("ticket.already_assigned", "Someone else is already working on this case.");
        Assign(agentId, agentId, now);
    }

    /// <summary>Moves the case to another team; the current assignee is released.</summary>
    public void MoveToTeam(Guid teamId, Guid? actorId, DateTimeOffset now)
    {
        if (IsFinal)
            throw new DomainException("ticket.not_assignable", "A closed case cannot be reassigned.");
        if (teamId == TeamId)
            return;
        Record(TicketEventType.Assigned, actorId, now, new { from = AssigneeId, to = (Guid?)null, fromTeamId = TeamId, teamId });
        TeamId = teamId;
        AssigneeId = null;
    }

    /// <summary>Starts the SLA clock with the policy's targets for the current priority. Paused at once in a pause status.</summary>
    public void StartSla(SlaPolicy policy, DateTimeOffset startedAt)
    {
        ArgumentNullException.ThrowIfNull(policy);
        if (SlaStartedAt is not null)
            throw new DomainException("sla.already_started", "The SLA clock has already started.");

        SlaPolicyId = policy.Id;
        SlaAtRiskPercent = policy.AtRiskThresholdPercent;
        SlaPauseStatuses = [.. policy.PauseStatuses];
        Retarget(policy);
        SlaStartedAt = startedAt;
        if (StopsClock(Status))
            _slaPauses.Add(new SlaPause(startedAt, null));
    }

    /// <summary>Takes the targets of the current priority (after a priority change).</summary>
    public void Retarget(SlaPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        var target = policy.TargetFor(Priority);
        FirstResponseTargetMinutes = target.FirstResponseMinutes;
        ResolutionTargetMinutes = target.ResolutionMinutes;
    }

    /// <summary>HR's first public reply stops the first-response clock.</summary>
    public void RecordFirstResponse(DateTimeOffset now) => FirstRespondedAt ??= now;

    /// <summary>
    /// Recomputes deadlines and the SLA state. A change of state is added to the audit trail and returned,
    /// so callers can notify and escalate.
    /// </summary>
    public (SlaState Previous, SlaState Current) RecalculateSla(IBusinessTimeCalculator calculator, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(calculator);
        var previous = SlaState;
        if (SlaStartedAt is not { } start || ResolutionTargetMinutes is not { } resolutionTarget || FirstResponseTargetMinutes is not { } responseTarget)
            return (previous, SlaState);

        var end = SlaStoppedAt is { } stopped && stopped < now ? stopped : now;
        var paused = IsSlaPaused || (SlaStoppedAt is not null && _slaPauses.Any(p => p.To is null));

        // Elapsed time excludes every pause; deadlines are only pushed back by pauses that are over
        // (an open pause - waiting, resolved - stops the clock without moving the deadline).
        int PausedUntil(DateTimeOffset until, bool completedOnly) => _slaPauses
            .Where(p => p.From < until && (!completedOnly || p.To is not null))
            .Sum(p => calculator.BusinessMinutesBetween(p.From, p.To is { } to && to < until ? to : until));

        // Resolution clock.
        var resolutionElapsed = calculator.BusinessMinutesBetween(start, end) - PausedUntil(end, completedOnly: false);
        ResolutionDueAt = paused && SlaStoppedAt is null
            ? null
            : calculator.AddBusinessMinutes(start, resolutionTarget + PausedUntil(end, completedOnly: true));
        var resolutionState = StateFor(resolutionElapsed, resolutionTarget);
        ResolutionBreached = resolutionState == SlaState.Breached;

        // First-response clock (stops at the first response).
        var responseEnd = FirstRespondedAt is { } responded && responded < end ? responded : end;
        var responseElapsed = calculator.BusinessMinutesBetween(start, responseEnd) - PausedUntil(responseEnd, completedOnly: false);
        FirstResponseDueAt = paused && FirstRespondedAt is null && SlaStoppedAt is null
            ? null
            : calculator.AddBusinessMinutes(start, responseTarget + PausedUntil(responseEnd, completedOnly: true));
        var responseState = StateFor(responseElapsed, responseTarget);
        FirstResponseBreached = responseState == SlaState.Breached;

        SlaState = FirstRespondedAt is null && responseState > resolutionState ? responseState : resolutionState;
        if (SlaState != previous && previous != SlaState.None)
            Record(TicketEventType.SlaStateChanged, null, now, new { from = previous.ToString(), to = SlaState.ToString() });
        return (previous, SlaState);
    }

    /// <summary>Business minutes since submission without any HR response (pauses excluded); null once answered.</summary>
    public int? BusinessMinutesWithoutResponse(IBusinessTimeCalculator calculator, DateTimeOffset now)
    {
        if (FirstRespondedAt is not null)
            return null;
        var end = SlaStoppedAt is { } stopped && stopped < now ? stopped : now;
        return ElapsedBusinessMinutes(calculator, end);
    }

    /// <summary>Business minutes from submission to <paramref name="until"/>, pauses excluded (null without SLA clock).</summary>
    public int? ElapsedBusinessMinutes(IBusinessTimeCalculator calculator, DateTimeOffset until)
    {
        ArgumentNullException.ThrowIfNull(calculator);
        if (SlaStartedAt is not { } start)
            return null;
        var paused = _slaPauses.Where(p => p.From < until)
            .Sum(p => calculator.BusinessMinutesBetween(p.From, p.To is { } to && to < until ? to : until));
        return Math.Max(0, calculator.BusinessMinutesBetween(start, until) - paused);
    }

    /// <summary>Raises the priority one level (no-op at Critical). Returns whether it changed.</summary>
    public bool RaisePriority(DateTimeOffset now)
    {
        if (Priority == TicketPriority.Critical)
            return false;
        ChangePriority(Priority + 1, actorId: null, now);
        return true;
    }

    private SlaState StateFor(int elapsedMinutes, int targetMinutes) =>
        elapsedMinutes >= targetMinutes ? SlaState.Breached
        : elapsedMinutes * 100L >= (long)targetMinutes * SlaAtRiskPercent ? SlaState.AtRisk
        : SlaState.OnTrack;

    /// <summary>The policy's pause statuses stop the clock, and so does Resolved (until reopened).</summary>
    private bool StopsClock(TicketStatus status) => status == TicketStatus.Resolved || SlaPauseStatuses.Contains(status);

    private void MoveSlaClock(TicketStatus from, TicketStatus to, DateTimeOffset now)
    {
        if (SlaStartedAt is null || SlaStoppedAt is not null)
            return;

        if (to == TicketStatus.InProgress || to == TicketStatus.Resolved)
            RecordFirstResponse(now);

        if (TicketStatusMachine.IsTerminal(to))
        {
            // Final: the clock stops where it is (an open pause, e.g. since resolution, stays open).
            SlaStoppedAt = now;
            return;
        }

        var open = _slaPauses.FindIndex(p => p.To is null);
        if (StopsClock(to) && open < 0)
            _slaPauses.Add(new SlaPause(now, null));
        else if (!StopsClock(to) && open >= 0)
            _slaPauses[open] = _slaPauses[open] with { To = now };
    }

    /// <summary>
    /// Removes the personal data of a closed case (GDPR): texts, answers and documents are erased, the
    /// requester is replaced by <paramref name="formerEmployeeId"/>, and free text in the trail is dropped.
    /// The structure (dates, statuses, SLA results) stays for statistics. Returns the storage keys to delete.
    /// </summary>
    public IReadOnlyList<string> Anonymize(Guid formerEmployeeId, DateTimeOffset now)
    {
        if (!IsFinal)
            throw new DomainException("ticket.not_closed", "Only closed cases can be anonymized.");
        if (AnonymizedAt is not null)
            return [];

        var previousRequester = RequesterId;
        Title = "Anonymized case";
        Description = string.Empty;
        FormData = "{}";
        RequesterId = formerEmployeeId;
        AnonymizedAt = now;

        foreach (var comment in _comments)
            comment.Redact(previousRequester, formerEmployeeId);
        foreach (var approval in _approvals)
            approval.Redact();
        foreach (var entry in _events)
            entry.Redact(previousRequester, formerEmployeeId);

        var keys = _attachments.Select(a => a.StorageKey).ToList();
        _attachments.Clear();
        return keys;
    }

    /// <summary>Appends an entry to the audit trail.</summary>
    public TicketEvent Record(TicketEventType type, Guid? actorId, DateTimeOffset now, object data, bool isInternal = false)
    {
        // One operation often records several events at the same instant (a reply and a status change).
        // Each gets a strictly later timestamp (1 µs, PostgreSQL's precision) so the trail sorts in the order things happened.
        var at = now > _lastEventAt ? now : _lastEventAt.AddTicks(10);
        _lastEventAt = at;
        var entry = new TicketEvent(Id, type, actorId, at, JsonSerializer.Serialize(data, JsonDefaults.Web), isInternal) { TenantId = TenantId };
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

        var comment = new Comment(Id, authorId, body, isInternal, now) { TenantId = TenantId };
        _comments.Add(comment);
        Record(TicketEventType.CommentAdded, authorId, now, new { commentId = comment.Id, isInternal }, isInternal);
        return comment;
    }

    public Attachment AddAttachment(
        string fileName, string contentType, long sizeBytes, string storageKey, Guid uploadedById, string? fieldKey, DateTimeOffset now)
    {
        var attachment = new Attachment(Id, fileName, contentType, sizeBytes, storageKey, uploadedById, fieldKey, now) { TenantId = TenantId };
        _attachments.Add(attachment);
        // Files answering a form field come with the submission and are part of the "Created" event.
        if (fieldKey is null)
            Record(TicketEventType.AttachmentAdded, uploadedById, now, new { fileName });
        return attachment;
    }
}
