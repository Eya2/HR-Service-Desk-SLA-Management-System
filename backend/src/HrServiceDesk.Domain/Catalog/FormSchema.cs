using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using HrServiceDesk.Domain.Common;

namespace HrServiceDesk.Domain.Catalog;

public sealed record FieldError(string Field, string Message);

/// <summary>Outcome of validating a submission: the cleaned values (strings trimmed, empties dropped) or errors.</summary>
public sealed record FormSubmission(JsonObject Values, IReadOnlyList<FieldError> Errors)
{
    public bool IsValid => Errors.Count == 0;
}

/// <summary>
/// The dynamic form of a request type, stored as JSON. <see cref="Parse"/> checks the definition;
/// <see cref="Validate"/> checks an employee's answers (files are counted separately, per field key).
/// </summary>
public sealed partial class FormSchema
{
    public const int MaxFields = 30;
    public const int MaxFilesPerField = 5;
    public const int DefaultTextMaxLength = 500;
    public const int DefaultTextareaMaxLength = 4000;

    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(100);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    private sealed record Document(IReadOnlyList<FormField>? Fields);

    private FormSchema(IReadOnlyList<FormField> fields) => Fields = fields;

    public IReadOnlyList<FormField> Fields { get; }

    public static FormSchema Empty { get; } = new([]);

    public static FormSchema Create(IEnumerable<FormField> fields)
    {
        var list = (fields ?? []).ToList();
        var problems = DefinitionErrors(list).ToList();
        if (problems.Count > 0)
            throw new DomainException("form_schema.invalid", string.Join(" ", problems));
        return new FormSchema(list);
    }

    public static FormSchema Parse(string json)
    {
        Document? document;
        try
        {
            document = JsonSerializer.Deserialize<Document>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new DomainException("form_schema.invalid", $"Form schema is not valid JSON: {ex.Message}");
        }

        return Create(document?.Fields ?? []);
    }

    public string ToJson() => JsonSerializer.Serialize(new Document(Fields), JsonOptions);

    public FormField? Field(string key) => Fields.FirstOrDefault(f => f.Key == key);

    /// <summary>Validates answers. <paramref name="values"/> holds non-file fields; <paramref name="fileCounts"/> the number of files per file field.</summary>
    public FormSubmission Validate(JsonObject? values, IReadOnlyDictionary<string, int> fileCounts)
    {
        values ??= [];
        var errors = new List<FieldError>();
        var cleaned = new JsonObject();

        foreach (var (key, _) in values)
        {
            var field = Field(key);
            if (field is null)
                errors.Add(new FieldError(key, "Unknown field."));
            else if (field.Type == FormFieldType.File)
                errors.Add(new FieldError(key, "Files must be uploaded, not sent as values."));
        }

        foreach (var key in fileCounts.Keys.Where(k => Field(k)?.Type != FormFieldType.File))
            errors.Add(new FieldError(key, "This field does not accept files."));

        foreach (var field in Fields)
        {
            if (field.Type == FormFieldType.File)
            {
                ValidateFiles(field, fileCounts.GetValueOrDefault(field.Key), errors);
                continue;
            }

            var node = values[field.Key];
            if (IsEmpty(node))
            {
                if (field.Required)
                    errors.Add(new FieldError(field.Key, $"{field.Label} is required."));
                continue;
            }

            var value = ValidateValue(field, node!, errors);
            if (value is not null)
                cleaned[field.Key] = value;
        }

        return new FormSubmission(cleaned, errors);
    }

    private static JsonNode? ValidateValue(FormField field, JsonNode node, List<FieldError> errors)
    {
        switch (field.Type)
        {
            case FormFieldType.Text:
            case FormFieldType.Textarea:
            {
                if (!TryGetString(node, out var text))
                    return Fail(errors, field, "must be text");
                text = text.Trim();
                var max = field.MaxLength ?? (field.Type == FormFieldType.Text ? DefaultTextMaxLength : DefaultTextareaMaxLength);
                if (field.MinLength is { } min && text.Length < min)
                    return Fail(errors, field, $"must be at least {min} characters");
                if (text.Length > max)
                    return Fail(errors, field, $"must be at most {max} characters");
                if (field.Pattern is { } pattern && !FullMatch(pattern, text))
                    return Fail(errors, field, "has an invalid format");
                return JsonValue.Create(text);
            }

            case FormFieldType.Number:
            {
                if (node is not JsonValue v || !v.TryGetValue<decimal>(out var number))
                    return Fail(errors, field, "must be a number");
                if (field.Min is { } min && number < min)
                    return Fail(errors, field, $"must be at least {min.ToString(CultureInfo.InvariantCulture)}");
                if (field.Max is { } max && number > max)
                    return Fail(errors, field, $"must be at most {max.ToString(CultureInfo.InvariantCulture)}");
                return JsonValue.Create(number);
            }

            case FormFieldType.Date:
            {
                if (!TryGetString(node, out var text) ||
                    !DateOnly.TryParseExact(text.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                {
                    return Fail(errors, field, "must be a date (YYYY-MM-DD)");
                }

                return JsonValue.Create(date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            }

            case FormFieldType.Select:
            {
                if (!TryGetString(node, out var choice) || field.Options!.All(o => o.Value != choice))
                    return Fail(errors, field, "must be one of the proposed options");
                return JsonValue.Create(choice);
            }

            default:
                return null;
        }
    }

    private static void ValidateFiles(FormField field, int count, List<FieldError> errors)
    {
        if (field.Required && count == 0)
            errors.Add(new FieldError(field.Key, $"{field.Label} is required."));
        var max = field.MaxFiles ?? 1;
        if (count > max)
            errors.Add(new FieldError(field.Key, $"{field.Label} accepts at most {max} file(s)."));
    }

    private static JsonNode? Fail(List<FieldError> errors, FormField field, string problem)
    {
        errors.Add(new FieldError(field.Key, $"{field.Label} {problem}."));
        return null;
    }

    private static bool IsEmpty(JsonNode? node) =>
        node is null || (node is JsonValue v && v.TryGetValue<string>(out var s) && string.IsNullOrWhiteSpace(s));

    private static bool TryGetString(JsonNode node, out string value)
    {
        value = string.Empty;
        return node is JsonValue v && v.TryGetValue(out value!);
    }

    private static bool FullMatch(string pattern, string text)
    {
        try
        {
            return Regex.IsMatch(text, $"^(?:{pattern})$", RegexOptions.CultureInvariant, RegexTimeout);
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }

    private static IEnumerable<string> DefinitionErrors(List<FormField> fields)
    {
        if (fields.Count > MaxFields)
            yield return $"A form can have at most {MaxFields} fields.";

        foreach (var duplicate in fields.GroupBy(f => f.Key).Where(g => g.Count() > 1))
            yield return $"Field key '{duplicate.Key}' is used more than once.";

        foreach (var field in fields)
        {
            if (field.Key is null || !KeyPattern().IsMatch(field.Key))
                yield return $"Field key '{field.Key}' must start with a letter and contain only letters, digits or '_' (max 50).";
            if (string.IsNullOrWhiteSpace(field.Label) || field.Label.Length > 200)
                yield return $"Field '{field.Key}' needs a label of 1 to 200 characters.";
            if (!Enum.IsDefined(field.Type))
                yield return $"Field '{field.Key}' has an unknown type.";

            if (field.Type == FormFieldType.Select)
            {
                if (field.Options is not { Count: > 0 })
                    yield return $"Select field '{field.Key}' needs at least one option.";
                else if (field.Options.Select(o => o.Value).Distinct().Count() != field.Options.Count)
                    yield return $"Select field '{field.Key}' has duplicate option values.";
            }

            if (field.Min is { } min && field.Max is { } max && min > max)
                yield return $"Field '{field.Key}' has min greater than max.";
            if (field.MinLength < 0 || field.MaxLength < 1 || (field.MinLength > field.MaxLength))
                yield return $"Field '{field.Key}' has invalid length bounds.";
            if (field.MaxFiles is < 1 or > MaxFilesPerField)
                yield return $"File field '{field.Key}' must allow 1 to {MaxFilesPerField} files.";
            if (field.Pattern is { } pattern && !IsValidRegex(pattern))
                yield return $"Field '{field.Key}' has an invalid pattern.";
        }
    }

    private static bool IsValidRegex(string pattern)
    {
        try
        {
            _ = new Regex(pattern, RegexOptions.None, RegexTimeout);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    [GeneratedRegex("^[a-zA-Z][a-zA-Z0-9_]{0,49}$")]
    private static partial Regex KeyPattern();
}
