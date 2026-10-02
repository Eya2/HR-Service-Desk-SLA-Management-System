using HrServiceDesk.Domain.Catalog;
using HrServiceDesk.Domain.Tickets;

namespace HrServiceDesk.Infrastructure.Persistence.Seeding;

/// <summary>The request types every demo organisation starts with.</summary>
internal static class DemoCatalog
{
    private static FormFieldOption[] Options(params (string Value, string Label)[] options) =>
        options.Select(o => new FormFieldOption(o.Value, o.Label)).ToArray();

    public static IEnumerable<RequestType> Create()
    {
        yield return RequestType.Create(
            "Work certificate",
            "An official certificate of employment (attestation de travail) for a bank, a visa or a landlord.",
            RequestCategory.Certificates, isConfidential: false, TicketPriority.Low,
            FormSchema.Create(
            [
                new FormField
                {
                    Key = "purpose", Label = "Purpose", Type = FormFieldType.Select, Required = true,
                    Options = Options(("bank", "Bank"), ("visa", "Visa / embassy"), ("housing", "Housing"), ("other", "Other")),
                },
                new FormField
                {
                    Key = "language", Label = "Language", Type = FormFieldType.Select, Required = true,
                    Options = Options(("fr", "French"), ("en", "English"), ("ar", "Arabic")),
                },
                new FormField { Key = "copies", Label = "Number of copies", Type = FormFieldType.Number, Required = true, Min = 1, Max = 5 },
                new FormField { Key = "neededBy", Label = "Needed by", Type = FormFieldType.Date },
            ]));

        yield return RequestType.Create(
            "Payslip correction",
            "Report an error on a payslip: missing overtime, wrong deduction, missing bonus…",
            RequestCategory.Payroll, isConfidential: false, TicketPriority.High,
            FormSchema.Create(
            [
                new FormField
                {
                    Key = "payPeriod", Label = "Pay period", Type = FormFieldType.Text, Required = true,
                    Pattern = "[0-9]{4}-(0[1-9]|1[0-2])", HelpText = "Format YYYY-MM, e.g. 2026-03",
                },
                new FormField
                {
                    Key = "issue", Label = "Issue", Type = FormFieldType.Select, Required = true,
                    Options = Options(
                        ("missing_overtime", "Missing overtime"), ("wrong_deduction", "Wrong deduction"),
                        ("missing_bonus", "Missing bonus"), ("wrong_hours", "Wrong hours worked"), ("other", "Other")),
                },
                new FormField { Key = "expectedAmount", Label = "Expected amount", Type = FormFieldType.Number, Min = 0, Max = 100000 },
                new FormField { Key = "payslip", Label = "Payslip", Type = FormFieldType.File, Required = true, MaxFiles = 1, HelpText = "PDF or photo of the payslip" },
            ]));

        yield return RequestType.Create(
            "Leave request",
            "Request annual, sick, unpaid or parental leave.",
            RequestCategory.LeaveAndAbsence, isConfidential: false, TicketPriority.Medium,
            FormSchema.Create(
            [
                new FormField
                {
                    Key = "leaveType", Label = "Leave type", Type = FormFieldType.Select, Required = true,
                    Options = Options(
                        ("annual", "Annual leave"), ("sick", "Sick leave"), ("unpaid", "Unpaid leave"),
                        ("parental", "Maternity / paternity leave"), ("other", "Other")),
                },
                new FormField { Key = "startDate", Label = "First day", Type = FormFieldType.Date, Required = true },
                new FormField { Key = "endDate", Label = "Last day", Type = FormFieldType.Date, Required = true },
                new FormField
                {
                    Key = "medicalCertificate", Label = "Medical certificate", Type = FormFieldType.File, MaxFiles = 2,
                    HelpText = "Required for sick leave",
                },
            ]));

        yield return RequestType.Create(
            "Salary advance",
            "Ask for part of your salary in advance, repaid through payroll deductions.",
            RequestCategory.Payroll, isConfidential: false, TicketPriority.Medium,
            FormSchema.Create(
            [
                new FormField { Key = "amount", Label = "Amount", Type = FormFieldType.Number, Required = true, Min = 100, Max = 5000 },
                new FormField { Key = "repaymentMonths", Label = "Repayment period (months)", Type = FormFieldType.Number, Required = true, Min = 1, Max = 12 },
                new FormField { Key = "reason", Label = "Reason", Type = FormFieldType.Textarea, Required = true, MaxLength = 1000 },
            ]));

        yield return RequestType.Create(
            "Change of bank details",
            "Update the bank account your salary is paid into.",
            RequestCategory.Payroll, isConfidential: false, TicketPriority.High,
            FormSchema.Create(
            [
                new FormField { Key = "bankName", Label = "Bank name", Type = FormFieldType.Text, Required = true, MaxLength = 100 },
                new FormField
                {
                    Key = "iban", Label = "IBAN", Type = FormFieldType.Text, Required = true,
                    Pattern = "[A-Z]{2}[0-9]{2}[A-Z0-9]{10,30}", HelpText = "Capital letters and digits, no spaces",
                },
                new FormField { Key = "bankCertificate", Label = "Bank account certificate (RIB)", Type = FormFieldType.File, Required = true, MaxFiles = 1 },
            ]));

        yield return RequestType.Create(
            "Training request",
            "Ask to attend a course, a certification or a conference.",
            RequestCategory.Training, isConfidential: false, TicketPriority.Low,
            FormSchema.Create(
            [
                new FormField { Key = "courseTitle", Label = "Course title", Type = FormFieldType.Text, Required = true, MaxLength = 200 },
                new FormField { Key = "provider", Label = "Provider", Type = FormFieldType.Text, Required = true, MaxLength = 200 },
                new FormField { Key = "startDate", Label = "Start date", Type = FormFieldType.Date, Required = true },
                new FormField { Key = "cost", Label = "Cost", Type = FormFieldType.Number, Required = true, Min = 0, Max = 50000 },
                new FormField { Key = "justification", Label = "How it helps your role", Type = FormFieldType.Textarea, Required = true, MaxLength = 2000 },
            ]));

        yield return RequestType.Create(
            "Harassment report",
            "Report harassment or discrimination in confidence. Only a restricted HR group can see these cases.",
            RequestCategory.Confidential, isConfidential: true, TicketPriority.High,
            FormSchema.Create(
            [
                new FormField { Key = "incidentDate", Label = "Date of the incident", Type = FormFieldType.Date, Required = true },
                new FormField { Key = "location", Label = "Location", Type = FormFieldType.Text, MaxLength = 200 },
                new FormField { Key = "whatHappened", Label = "What happened", Type = FormFieldType.Textarea, Required = true, MinLength = 20 },
                new FormField { Key = "witnesses", Label = "Witnesses", Type = FormFieldType.Textarea, MaxLength = 1000 },
                new FormField { Key = "evidence", Label = "Evidence", Type = FormFieldType.File, MaxFiles = 5 },
            ]));
    }
}
