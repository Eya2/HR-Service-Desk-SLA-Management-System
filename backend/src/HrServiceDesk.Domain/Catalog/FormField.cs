using System.Text.Json.Serialization;

namespace HrServiceDesk.Domain.Catalog;

[JsonConverter(typeof(JsonStringEnumConverter<FormFieldType>))]
public enum FormFieldType
{
    Text,
    Textarea,
    Number,
    Date,
    Select,
    File,
}

public sealed record FormFieldOption(string Value, string Label);

/// <summary>One input of a request type's dynamic form. Only the constraints relevant to <see cref="Type"/> are used.</summary>
public sealed record FormField
{
    public required string Key { get; init; }
    public required string Label { get; init; }
    public required FormFieldType Type { get; init; }
    public bool Required { get; init; }
    public string? HelpText { get; init; }

    /// <summary>Choices for <see cref="FormFieldType.Select"/>.</summary>
    public IReadOnlyList<FormFieldOption>? Options { get; init; }

    /// <summary>Bounds for <see cref="FormFieldType.Number"/>.</summary>
    public decimal? Min { get; init; }
    public decimal? Max { get; init; }

    /// <summary>Length bounds and an optional full-match regular expression for text fields.</summary>
    public int? MinLength { get; init; }
    public int? MaxLength { get; init; }
    public string? Pattern { get; init; }

    /// <summary>Maximum number of files for <see cref="FormFieldType.File"/> (default 1).</summary>
    public int? MaxFiles { get; init; }
}
