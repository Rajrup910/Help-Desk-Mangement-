namespace HelpDesk.Api.Models;

/// <summary>
/// Single source of truth for how long a ticket may stay unresolved.
/// Centralised so the seeder, the create path and the tests cannot drift apart.
/// </summary>
public static class SlaPolicy
{
    public static TimeSpan ResolutionWindowFor(TicketPriority priority) => priority switch
    {
        TicketPriority.Critical => TimeSpan.FromHours(4),
        TicketPriority.High => TimeSpan.FromHours(24),
        TicketPriority.Medium => TimeSpan.FromHours(72),
        TicketPriority.Low => TimeSpan.FromDays(7),
        _ => TimeSpan.FromDays(7)
    };

    public static DateTime DueDateFor(TicketPriority priority, DateTime createdAtUtc) =>
        createdAtUtc.Add(ResolutionWindowFor(priority));
}
