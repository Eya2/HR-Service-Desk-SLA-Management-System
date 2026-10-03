using System.Text.Json.Nodes;
using HrServiceDesk.Application.Assistant;
using HrServiceDesk.Domain.Catalog;
using HrServiceDesk.Domain.Tickets;

namespace HrServiceDesk.Application.Tests.Assistant;

public class LocalClassifierTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 3, 9, 0, 0, TimeSpan.Zero);

    private static readonly RequestType Payslip = RequestType.Create(
        "Payslip correction", "Report an error on a payslip: missing overtime, wrong deduction, missing bonus…", RequestCategory.Payroll, false, TicketPriority.High,
        FormSchema.Create(
        [
            new FormField { Key = "payPeriod", Label = "Pay period", Type = FormFieldType.Text, Pattern = "[0-9]{4}-(0[1-9]|1[0-2])" },
            new FormField
            {
                Key = "issue", Label = "Issue", Type = FormFieldType.Select,
                Options = [new("missing_overtime", "Missing overtime"), new("wrong_deduction", "Wrong deduction"), new("missing_bonus", "Missing bonus")],
            },
            new FormField { Key = "expectedAmount", Label = "Expected amount", Type = FormFieldType.Number, Min = 0, Max = 100000 },
            new FormField { Key = "payslip", Label = "Payslip", Type = FormFieldType.File },
        ]));

    private static readonly RequestType Certificate = RequestType.Create(
        "Work certificate", "An official certificate of employment for a bank, a visa or a landlord.", RequestCategory.Certificates, false, TicketPriority.Low,
        FormSchema.Create(
        [
            new FormField { Key = "purpose", Label = "Purpose", Type = FormFieldType.Select, Options = [new("bank", "Bank"), new("visa", "Visa / embassy"), new("housing", "Housing")] },
        ]));

    private static readonly RequestType Leave = RequestType.Create(
        "Leave request", "Request annual, sick, unpaid or parental leave.", RequestCategory.LeaveAndAbsence, false, TicketPriority.Medium,
        FormSchema.Create([new FormField { Key = "leaveType", Label = "Leave type", Type = FormFieldType.Select, Options = [new("annual", "Annual leave"), new("sick", "Sick leave")] }]));

    private static readonly RequestType[] Catalog = [Payslip, Certificate, Leave];

    [Theory]
    [InlineData("Ma prime n'est pas sur mon bulletin de paie", "Payslip correction")]
    [InlineData("Il me faut une attestation de travail pour l'ambassade", "Work certificate")]
    [InlineData("Je voudrais poser mes congés pour les vacances d'été", "Leave request")]
    [InlineData("Could I get an employment certificate for my landlord?", "Work certificate")]
    public void Ranks_the_matching_type_first(string text, string expected) =>
        LocalClassifier.Rank(text, Catalog)[0].Type.Name.Should().Be(expected);

    [Fact]
    public void Unrelated_text_ranks_nothing_and_has_no_confidence()
    {
        var ranked = LocalClassifier.Rank("Bonjour", Catalog);

        ranked.Should().BeEmpty();
        LocalClassifier.Confidence(ranked).Should().Be(0);
    }

    [Fact]
    public void A_clear_winner_is_more_confident_than_a_close_call()
    {
        var clear = LocalClassifier.Confidence(LocalClassifier.Rank("congé maladie, je suis malade", Catalog));
        var close = LocalClassifier.Confidence(LocalClassifier.Rank("bank", Catalog));

        clear.Should().BeGreaterThan(close);
    }

    [Fact]
    public void Prefills_choices_pay_period_and_amount_but_never_files()
    {
        var values = LocalClassifier.Prefill("Il manque mes heures sup de septembre, environ 320,5 dinars", Payslip, Now);

        values["issue"]!.GetValue<string>().Should().Be("missing_overtime");
        values["payPeriod"]!.GetValue<string>().Should().Be("2026-09");
        values["expectedAmount"]!.GetValue<decimal>().Should().Be(320.5m);
        values.Should().NotContainKey("payslip");
    }

    [Theory]
    [InlineData("my November payslip", "2025-11")] // a month still to come this year means last year's
    [InlineData("fiche de paie de mars 2024", "2024-03")]
    [InlineData("payslip 04/2026", "2026-04")]
    public void Reads_the_pay_period(string text, string expected) =>
        LocalClassifier.Prefill(text, Payslip, Now)["payPeriod"]!.GetValue<string>().Should().Be(expected);

    [Fact]
    public void May_alone_is_not_read_as_a_month() =>
        LocalClassifier.Prefill("I may have a payslip error", Payslip, Now).Should().NotContainKey("payPeriod");

    [Theory]
    [InlineData("Mes heures sup manquent. Merci de vérifier.", "Mes heures sup manquent.")]
    [InlineData("attestation de travail", "Attestation de travail")]
    [InlineData("Mes heures sup de septembre n'apparaissent pas sur ma fiche de paie, il manque environ 320 dinars", "Mes heures sup de septembre n'apparaissent pas sur ma fiche de paie")]
    public void Titles_come_from_the_first_sentence(string text, string expected) =>
        LocalClassifier.Title(text).Should().Be(expected);

    [Fact]
    public void Long_titles_are_cut_on_a_word()
    {
        var title = LocalClassifier.Title(string.Join(' ', Enumerable.Repeat("overtime", 20)));

        title.Length.Should().BeLessThanOrEqualTo(90);
        title.Should().EndWith("overtime…");
    }

    [Theory]
    [InlineData("Bonjour, je n'ai pas reçu mon attestation", true)]
    [InlineData("Hello, I have not received my certificate", false)]
    public void Tells_french_from_english(string text, bool french) => LocalClassifier.LooksFrench(text).Should().Be(french);

    [Fact]
    public void Model_answers_are_kept_only_when_the_form_accepts_them()
    {
        var values = new JsonObject
        {
            ["issue"] = "missing_overtime",
            ["payPeriod"] = "March",
            ["expectedAmount"] = 250,
            ["payslip"] = "payslip.pdf",
            ["invented"] = "x",
        };

        var clean = AssistantHandlers.Sanitize(values, Payslip.Schema);

        clean.Select(v => v.Key).Should().BeEquivalentTo("issue", "expectedAmount");
        AssistantHandlers.Sanitize(new JsonObject { ["issue"] = "free_lunch", ["expectedAmount"] = -5 }, Payslip.Schema).Should().BeEmpty();
    }
}
