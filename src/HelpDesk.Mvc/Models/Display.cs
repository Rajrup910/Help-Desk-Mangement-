using System.Globalization;
using HelpDesk.Contracts;

namespace HelpDesk.Mvc.Models;

/// <summary>Helper methods used by the views for labels, colours and dates.</summary>
public static class Display
{
    /// <summary>"InProgress" reads badly on screen, so show it as "In Progress".</summary>
    public static string Label(TicketStatus status) =>
        status == TicketStatus.InProgress ? "In Progress" : status.ToString();

    /// <summary>CSS class that colours the status label.</summary>
    public static string StatusClass(TicketStatus status) => status switch
    {
        TicketStatus.Open => "open",
        TicketStatus.InProgress => "progress",
        TicketStatus.Resolved => "resolved",
        TicketStatus.Closed => "closed",
        _ => "open"
    };

    /// <summary>CSS class that colours the priority label.</summary>
    public static string PriorityClass(TicketPriority priority) => priority switch
    {
        TicketPriority.Low => "low",
        TicketPriority.Medium => "medium",
        TicketPriority.High => "high",
        TicketPriority.Critical => "critical",
        _ => "low"
    };

    /// <summary>The API sends UTC times, so convert them to the local time zone for display.</summary>
    public static string DateTimeText(DateTime utc) =>
        ToLocal(utc).ToString("dd MMM yyyy, HH:mm", CultureInfo.InvariantCulture);

    /// <summary>Short text describing the SLA deadline, e.g. "2 days left" or "Overdue".</summary>
    public static string DueText(TicketDto ticket)
    {
        if (ticket.ResolvedAt is not null || ticket.Status is TicketStatus.Resolved or TicketStatus.Closed)
        {
            return "Completed";
        }

        var remaining = EnsureUtc(ticket.DueDate) - DateTime.UtcNow;

        if (remaining < TimeSpan.Zero)
        {
            return "Overdue";
        }

        if (remaining.TotalHours < 1)
        {
            return $"{(int)remaining.TotalMinutes} min left";
        }

        if (remaining.TotalHours < 48)
        {
            return $"{(int)remaining.TotalHours} hours left";
        }

        return $"{(int)remaining.TotalDays} days left";
    }

    /// <summary>Cuts long text so the table columns stay tidy.</summary>
    public static string Short(string value, int maxLength) =>
        string.IsNullOrEmpty(value) || value.Length <= maxLength
            ? value
            : value.Substring(0, maxLength) + "...";

    private static DateTime ToLocal(DateTime utc) => EnsureUtc(utc).ToLocalTime();

    private static DateTime EnsureUtc(DateTime value) =>
        value.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(value, DateTimeKind.Utc)
            : value.ToUniversalTime();
}
