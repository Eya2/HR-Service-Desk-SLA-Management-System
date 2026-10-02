using HrServiceDesk.Domain.Common;
using HrServiceDesk.Domain.Tickets;

namespace HrServiceDesk.Domain.Catalog;

/// <summary>An entry of the HR request catalog, with the dynamic form employees fill in.</summary>
public sealed class RequestType : Entity, ITenantOwned, IAuditable
{
    public const int NameMaxLength = 100;
    public const int DescriptionMaxLength = 500;

    private RequestType() { }

    public Guid TenantId { get; set; }

    public string Name { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public RequestCategory Category { get; private set; }

    /// <summary>Cases of a confidential type are visible only to the requester and a restricted HR group.</summary>
    public bool IsConfidential { get; private set; }

    public TicketPriority DefaultPriority { get; private set; } = TicketPriority.Medium;

    /// <summary>Cases hold sensitive personal data (bank details, health…): every view by HR is audited.</summary>
    public bool IsSensitive { get; private set; }

    public bool IsActive { get; private set; } = true;

    /// <summary>The <see cref="FormSchema"/> as JSON (jsonb column).</summary>
    public string FormSchemaJson { get; private set; } = FormSchema.Empty.ToJson();

    /// <summary>The team that handles cases of this type; null leaves them in the general HR queue.</summary>
    public Guid? ResponsibleTeamId { get; private set; }

    /// <summary>The SLA policy of this type; null means the tenant's default policy.</summary>
    public Guid? SlaPolicyId { get; private set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public FormSchema Schema => FormSchema.Parse(FormSchemaJson);

    public static RequestType Create(
        string name, string description, RequestCategory category, bool isConfidential, TicketPriority defaultPriority, FormSchema schema)
    {
        var type = new RequestType();
        type.Update(name, description, category, isConfidential, defaultPriority, schema);
        return type;
    }

    public void Update(
        string name, string description, RequestCategory category, bool isConfidential, TicketPriority defaultPriority, FormSchema schema)
    {
        ArgumentNullException.ThrowIfNull(schema);
        name = (name ?? string.Empty).Trim();
        description = (description ?? string.Empty).Trim();
        if (name.Length is 0 or > NameMaxLength)
            throw new DomainException("request_type.invalid_name", $"Name must be 1 to {NameMaxLength} characters.");
        if (description.Length > DescriptionMaxLength)
            throw new DomainException("request_type.invalid_description", $"Description must be at most {DescriptionMaxLength} characters.");

        Name = name;
        Description = description;
        Category = category;
        // The Confidential category always implies restricted visibility.
        IsConfidential = isConfidential || category == RequestCategory.Confidential;
        DefaultPriority = defaultPriority;
        FormSchemaJson = schema.ToJson();
    }

    public void SetResponsibleTeam(Guid? teamId) => ResponsibleTeamId = teamId;

    public void SetSlaPolicy(Guid? policyId) => SlaPolicyId = policyId;

    public void MarkSensitive(bool isSensitive) => IsSensitive = isSensitive;

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;
}
