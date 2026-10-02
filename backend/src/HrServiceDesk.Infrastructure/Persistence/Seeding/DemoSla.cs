using HrServiceDesk.Domain.Sla;
using HrServiceDesk.Domain.Tickets;

namespace HrServiceDesk.Infrastructure.Persistence.Seeding;

/// <summary>
/// Demo business calendars (Tunisia, France) with 2026–2027 public holidays, and SLA policies.
/// Islamic holidays follow the lunar calendar: their dates are estimates that HR Admins adjust once announced.
/// </summary>
internal static class DemoSla
{
    private static readonly DayOfWeek[] Weekdays =
        [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday];

    private static Holiday H(int y, int m, int d, string name) => new(new DateOnly(y, m, d), name);

    public static BusinessCalendar Tunisia() => BusinessCalendar.Create(
        "Tunisia",
        "Africa/Tunis",
        Weekdays.SelectMany(d => new[]
        {
            new WorkingInterval(d, new TimeOnly(8, 0), new TimeOnly(12, 0)),
            new WorkingInterval(d, new TimeOnly(13, 0), new TimeOnly(17, 0)),
        }),
        [
            H(2026, 1, 1, "New Year's Day"), H(2026, 3, 20, "Independence Day"), H(2026, 3, 21, "Eid al-Fitr (estimated)"),
            H(2026, 3, 22, "Eid al-Fitr, 2nd day (estimated)"), H(2026, 4, 9, "Martyrs' Day"), H(2026, 5, 1, "Labour Day"),
            H(2026, 5, 27, "Eid al-Adha (estimated)"), H(2026, 5, 28, "Eid al-Adha, 2nd day (estimated)"),
            H(2026, 6, 16, "Islamic New Year (estimated)"), H(2026, 7, 25, "Republic Day"), H(2026, 8, 13, "Women's Day"),
            H(2026, 8, 25, "Mawlid (estimated)"), H(2026, 10, 15, "Evacuation Day"), H(2026, 12, 17, "Revolution Day"),
            H(2027, 1, 1, "New Year's Day"), H(2027, 3, 10, "Eid al-Fitr (estimated)"), H(2027, 3, 11, "Eid al-Fitr, 2nd day (estimated)"),
            H(2027, 3, 20, "Independence Day"), H(2027, 4, 9, "Martyrs' Day"), H(2027, 5, 1, "Labour Day"),
            H(2027, 5, 16, "Eid al-Adha (estimated)"), H(2027, 5, 17, "Eid al-Adha, 2nd day (estimated)"),
            H(2027, 6, 6, "Islamic New Year (estimated)"), H(2027, 7, 25, "Republic Day"), H(2027, 8, 13, "Women's Day"),
            H(2027, 8, 15, "Mawlid (estimated)"), H(2027, 10, 15, "Evacuation Day"), H(2027, 12, 17, "Revolution Day"),
        ]);

    public static BusinessCalendar France() => BusinessCalendar.Create(
        "France",
        "Europe/Paris",
        Weekdays.SelectMany(d => new[]
        {
            new WorkingInterval(d, new TimeOnly(9, 0), new TimeOnly(12, 30)),
            new WorkingInterval(d, new TimeOnly(13, 30), new TimeOnly(18, 0)),
        }),
        [
            H(2026, 1, 1, "Jour de l'an"), H(2026, 4, 6, "Lundi de Pâques"), H(2026, 5, 1, "Fête du Travail"),
            H(2026, 5, 8, "Victoire 1945"), H(2026, 5, 14, "Ascension"), H(2026, 5, 25, "Lundi de Pentecôte"),
            H(2026, 7, 14, "Fête nationale"), H(2026, 8, 15, "Assomption"), H(2026, 11, 1, "Toussaint"),
            H(2026, 11, 11, "Armistice 1918"), H(2026, 12, 25, "Noël"),
            H(2027, 1, 1, "Jour de l'an"), H(2027, 3, 29, "Lundi de Pâques"), H(2027, 5, 1, "Fête du Travail"),
            H(2027, 5, 6, "Ascension"), H(2027, 5, 8, "Victoire 1945"), H(2027, 5, 17, "Lundi de Pentecôte"),
            H(2027, 7, 14, "Fête nationale"), H(2027, 8, 15, "Assomption"), H(2027, 11, 1, "Toussaint"),
            H(2027, 11, 11, "Armistice 1918"), H(2027, 12, 25, "Noël"),
        ]);

    private static readonly TicketStatus[] DefaultPauses = [TicketStatus.PendingApproval, TicketStatus.WaitingOnEmployee];

    /// <summary>Default policy, in business minutes (8 business hours = 480).</summary>
    public static SlaPolicy Standard()
    {
        var policy = SlaPolicy.Create("Standard",
        [
            new SlaTarget(TicketPriority.Low, 480, 2400),
            new SlaTarget(TicketPriority.Medium, 240, 1440),
            new SlaTarget(TicketPriority.High, 120, 480),
            new SlaTarget(TicketPriority.Critical, 60, 240),
        ], 80, DefaultPauses);
        policy.MarkAsDefault(true);
        return policy;
    }

    /// <summary>Tighter targets for pay issues.</summary>
    public static SlaPolicy Payroll() => SlaPolicy.Create("Payroll",
    [
        new SlaTarget(TicketPriority.Low, 240, 960),
        new SlaTarget(TicketPriority.Medium, 120, 480),
        new SlaTarget(TicketPriority.High, 60, 240),
        new SlaTarget(TicketPriority.Critical, 30, 120),
    ], 80, DefaultPauses);

    public static readonly string[] PayrollRequestTypes = ["Payslip correction", "Salary advance", "Change of bank details"];
}
