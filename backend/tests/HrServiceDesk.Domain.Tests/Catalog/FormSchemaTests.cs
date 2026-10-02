using System.Text.Json.Nodes;
using HrServiceDesk.Domain.Catalog;
using HrServiceDesk.Domain.Common;

namespace HrServiceDesk.Domain.Tests.Catalog;

public class FormSchemaTests
{
    private static readonly Dictionary<string, int> NoFiles = [];

    private static readonly FormSchema Schema = FormSchema.Create(
    [
        new FormField { Key = "period", Label = "Pay period", Type = FormFieldType.Text, Required = true, Pattern = "[0-9]{4}-(0[1-9]|1[0-2])" },
        new FormField { Key = "comment", Label = "Comment", Type = FormFieldType.Textarea, MinLength = 5, MaxLength = 20 },
        new FormField { Key = "amount", Label = "Amount", Type = FormFieldType.Number, Min = 100, Max = 5000 },
        new FormField { Key = "start", Label = "Start", Type = FormFieldType.Date },
        new FormField
        {
            Key = "kind", Label = "Kind", Type = FormFieldType.Select, Required = true,
            Options = [new("a", "Option A"), new("b", "Option B")],
        },
        new FormField { Key = "proof", Label = "Proof", Type = FormFieldType.File, Required = true, MaxFiles = 2 },
    ]);

    private static readonly Dictionary<string, int> OneProof = new() { ["proof"] = 1 };

    private static JsonObject Valid() => new() { ["period"] = "2026-03", ["kind"] = "a" };

    private static IEnumerable<string> ErrorsFor(JsonObject values, Dictionary<string, int>? files = null) =>
        Schema.Validate(values, files ?? OneProof).Errors.Select(e => $"{e.Field}: {e.Message}");

    [Fact]
    public void Valid_submission_passes_and_drops_empty_optional_values()
    {
        var values = Valid();
        values["comment"] = "   ";

        var result = Schema.Validate(values, OneProof);

        result.IsValid.Should().BeTrue();
        result.Values.Select(kv => kv.Key).Should().BeEquivalentTo("period", "kind");
    }

    [Fact]
    public void Required_fields_and_files_must_be_present()
    {
        ErrorsFor([], NoFiles).Should().BeEquivalentTo(
            "period: Pay period is required.", "kind: Kind is required.", "proof: Proof is required.");
    }

    [Fact]
    public void Text_is_trimmed_and_checked_against_length_and_full_pattern()
    {
        var values = Valid();
        values["period"] = " 2026-03 ";
        Schema.Validate(values, OneProof).Values["period"]!.GetValue<string>().Should().Be("2026-03");

        values["period"] = "2026-13";
        ErrorsFor(values).Should().ContainSingle().Which.Should().Contain("invalid format");

        values["period"] = "x2026-03"; // the pattern must match the whole value
        ErrorsFor(values).Should().ContainSingle().Which.Should().Contain("invalid format");

        values = Valid();
        values["comment"] = "abc";
        ErrorsFor(values).Should().ContainSingle().Which.Should().Contain("at least 5");
        values["comment"] = new string('x', 21);
        ErrorsFor(values).Should().ContainSingle().Which.Should().Contain("at most 20");
    }

    [Theory]
    [InlineData(99, "at least 100")]
    [InlineData(5001, "at most 5000")]
    public void Numbers_respect_bounds(decimal amount, string expected)
    {
        var values = Valid();
        values["amount"] = amount;

        ErrorsFor(values).Should().ContainSingle().Which.Should().Contain(expected);
    }

    [Fact]
    public void Numbers_must_be_json_numbers()
    {
        var values = Valid();
        values["amount"] = "150";

        ErrorsFor(values).Should().ContainSingle().Which.Should().Contain("must be a number");
    }

    [Theory]
    [InlineData("2026-02-30")]
    [InlineData("02/03/2026")]
    [InlineData("tomorrow")]
    public void Dates_must_be_real_iso_dates(string date)
    {
        var values = Valid();
        values["start"] = date;

        ErrorsFor(values).Should().ContainSingle().Which.Should().Contain("must be a date");
    }

    [Fact]
    public void Select_accepts_only_declared_values()
    {
        var values = Valid();
        values["kind"] = "Option A"; // the label is not a value

        ErrorsFor(values).Should().ContainSingle().Which.Should().Contain("proposed options");
    }

    [Fact]
    public void Unknown_fields_and_misplaced_files_are_rejected()
    {
        var values = Valid();
        values["hacker"] = "x";
        values["proof"] = "a-file-id";

        ErrorsFor(values, new Dictionary<string, int> { ["proof"] = 3, ["kind"] = 1 }).Should().BeEquivalentTo(
            "hacker: Unknown field.",
            "proof: Files must be uploaded, not sent as values.",
            "kind: This field does not accept files.",
            "proof: Proof accepts at most 2 file(s).");
    }

    [Fact]
    public void Definition_errors_are_all_reported()
    {
        var act = () => FormSchema.Create(
        [
            new FormField { Key = "1bad", Label = "A", Type = FormFieldType.Text },
            new FormField { Key = "dup", Label = "B", Type = FormFieldType.Text },
            new FormField { Key = "dup", Label = "C", Type = FormFieldType.Text },
            new FormField { Key = "choice", Label = "D", Type = FormFieldType.Select },
            new FormField { Key = "range", Label = "E", Type = FormFieldType.Number, Min = 10, Max = 1 },
            new FormField { Key = "regex", Label = "F", Type = FormFieldType.Text, Pattern = "([" },
            new FormField { Key = "files", Label = "G", Type = FormFieldType.File, MaxFiles = 9 },
            new FormField { Key = "nolabel", Label = " ", Type = FormFieldType.Text },
        ]);

        var message = act.Should().Throw<DomainException>().Which.Message;
        message.Should().Contain("'1bad'").And.Contain("'dup' is used more than once").And.Contain("'choice' needs at least one option")
            .And.Contain("'range' has min greater than max").And.Contain("'regex' has an invalid pattern")
            .And.Contain("'files' must allow 1 to 5").And.Contain("'nolabel' needs a label");
    }

    [Fact]
    public void Json_round_trip_preserves_the_definition()
    {
        var copy = FormSchema.Parse(Schema.ToJson());

        copy.Fields.Should().BeEquivalentTo(Schema.Fields);
        Schema.ToJson().Should().Contain("\"type\":\"Select\"").And.NotContain("\"min\":null");
    }

    [Fact]
    public void Parse_rejects_malformed_json()
    {
        var act = () => FormSchema.Parse("{ not json");

        act.Should().Throw<DomainException>().Which.Code.Should().Be("form_schema.invalid");
    }
}
