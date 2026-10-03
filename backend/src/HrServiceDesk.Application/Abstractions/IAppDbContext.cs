using HrServiceDesk.Domain.Audit;
using HrServiceDesk.Domain.Catalog;
using HrServiceDesk.Domain.Escalations;
using HrServiceDesk.Domain.Integration;
using HrServiceDesk.Domain.Knowledge;
using HrServiceDesk.Domain.Notifications;
using HrServiceDesk.Domain.Sla;
using HrServiceDesk.Domain.Teams;
using HrServiceDesk.Domain.Tenants;
using HrServiceDesk.Domain.Tickets;
using HrServiceDesk.Domain.Users;
using HrServiceDesk.Domain.Workflows;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HrServiceDesk.Application.Abstractions;

/// <summary>Unit of work and query surface used by use-case handlers.</summary>
public interface IAppDbContext
{
    DbSet<Tenant> Tenants { get; }

    DbSet<User> Users { get; }

    DbSet<RefreshToken> RefreshTokens { get; }

    DbSet<SsoConfiguration> SsoConfigurations { get; }

    DbSet<SsoLoginAttempt> SsoLoginAttempts { get; }

    DbSet<PasswordResetToken> PasswordResetTokens { get; }

    DbSet<RequestType> RequestTypes { get; }

    DbSet<Ticket> Tickets { get; }

    DbSet<Comment> Comments { get; }

    DbSet<Attachment> Attachments { get; }

    DbSet<TicketEvent> TicketEvents { get; }

    DbSet<TicketApproval> TicketApprovals { get; }

    DbSet<WorkflowDefinition> WorkflowDefinitions { get; }

    DbSet<Team> Teams { get; }

    DbSet<BusinessCalendar> BusinessCalendars { get; }

    DbSet<SlaPolicy> SlaPolicies { get; }

    DbSet<Notification> Notifications { get; }

    DbSet<EscalationRule> EscalationRules { get; }

    DbSet<EscalationExecution> EscalationExecutions { get; }

    DbSet<AuditLog> AuditLogs { get; }

    DbSet<KnowledgeArticle> KnowledgeArticles { get; }

    DbSet<SatisfactionRating> SatisfactionRatings { get; }

    DbSet<ApiKey> ApiKeys { get; }

    DbSet<WebhookSubscription> WebhookSubscriptions { get; }

    DbSet<WebhookDelivery> WebhookDeliveries { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>Starts a transaction spanning raw SQL (e.g. reference allocation) and <see cref="SaveChangesAsync"/>.</summary>
    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
}
