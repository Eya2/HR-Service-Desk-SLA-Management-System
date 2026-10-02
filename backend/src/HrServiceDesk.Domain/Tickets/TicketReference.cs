using System.Globalization;

namespace HrServiceDesk.Domain.Tickets;

/// <summary>Human-readable case numbers such as <c>HR-2026-000123</c>, sequential per tenant and year.</summary>
public static class TicketReference
{
    public static string Format(int year, long number)
    {
        if (year is < 2000 or > 9999)
            throw new ArgumentOutOfRangeException(nameof(year));
        if (number is < 1 or > 999_999)
            throw new ArgumentOutOfRangeException(nameof(number), "The yearly sequence supports up to 999,999 cases.");
        return string.Create(CultureInfo.InvariantCulture, $"HR-{year}-{number:D6}");
    }
}
