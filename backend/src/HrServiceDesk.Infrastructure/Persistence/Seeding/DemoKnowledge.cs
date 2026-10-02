using HrServiceDesk.Domain.Catalog;
using HrServiceDesk.Domain.Knowledge;

namespace HrServiceDesk.Infrastructure.Persistence.Seeding;

/// <summary>Help-centre articles of the demo organisations.</summary>
internal static class DemoKnowledge
{
    public static IEnumerable<KnowledgeArticle> Create() =>
    [
        KnowledgeArticle.Create(
            "When is salary paid, and how do I read my payslip?",
            "Pay day, where to find your payslip and what each line means.",
            "Salaries are paid on the last working day of the month. Your payslip is available in the HR portal the day before.\n\n"
            + "Gross salary is your contractual pay. Deductions include social security and income tax. Overtime approved by your manager "
            + "appears the following month.\n\nIf an amount looks wrong, wait until the next pay day before asking for a payslip correction: "
            + "late overtime is often added then.",
            RequestCategory.Payroll, publish: true),
        KnowledgeArticle.Create(
            "How to get a work certificate (attestation de travail)",
            "Certificates for banks, embassies or landlords, usually ready in two days.",
            "Choose \"Work certificate\" in the catalog, select the purpose and the language, and say how many copies you need.\n\n"
            + "Certificates are signed by HR and sent as PDF within two business days. Embassies sometimes need the original: "
            + "say so in the details and collect it from the HR office.",
            RequestCategory.Certificates, publish: true),
        KnowledgeArticle.Create(
            "Requesting annual leave",
            "How many days you have, notice to give, and approval.",
            "Submit a \"Leave request\" at least two weeks before the first day. Your manager approves it; HR then updates your balance.\n\n"
            + "Sick leave needs a medical certificate within 48 hours: attach it to the request. Leave balances are reset on 1 January.",
            RequestCategory.LeaveAndAbsence, publish: true),
        KnowledgeArticle.Create(
            "Changing the bank account your salary is paid into",
            "The document payroll needs and when the change takes effect.",
            "Use \"Change of bank details\" and attach a bank account certificate (RIB) in your name.\n\n"
            + "Changes received before the 20th apply to the current month's salary; later ones apply the next month. For your security, "
            + "payroll never accepts bank details sent by e-mail.",
            RequestCategory.Payroll, publish: true),
        KnowledgeArticle.Create(
            "Training budget and how to request a course",
            "Who can attend training, the yearly budget and the approval steps.",
            "Every employee can request training related to their role. Your manager approves first, then HR checks the budget.\n\n"
            + "Ask at least a month before the start date and attach the provider's programme and price.",
            RequestCategory.Training, publish: true),
        KnowledgeArticle.Create(
            "Reporting harassment or discrimination safely",
            "Your report is confidential and handled by a restricted HR group.",
            "Use \"Harassment report\". Only you and a small, trained HR group can see the case: your manager and colleagues cannot.\n\n"
            + "Describe what happened, when and who witnessed it. You can add evidence. Retaliation against someone who reports in good "
            + "faith is forbidden. In an emergency, contact the security team or emergency services first.",
            RequestCategory.Confidential, publish: true),
    ];
}
