using System.ComponentModel.DataAnnotations;

namespace HelpDesk.Api.Models;

/// <summary>A support request raised by an employee.</summary>
public class Ticket
{
    public int Id { get; set; }

    [Required, StringLength(120, MinimumLength = 5)]
    public string Title { get; set; } = string.Empty;

    [Required, StringLength(2000, MinimumLength = 10)]
    public string Description { get; set; } = string.Empty;

    public TicketPriority Priority { get; set; } = TicketPriority.Medium;

    public TicketStatus Status { get; set; } = TicketStatus.Open;

    public TicketCategory Category { get; set; } = TicketCategory.Other;

    [Required, StringLength(80)]
    public string RaisedBy { get; set; } = string.Empty;

    [EmailAddress, StringLength(120)]
    public string? RaisedByEmail { get; set; }

    [StringLength(80)]
    public string? AssignedTo { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>SLA deadline, derived from <see cref="Priority"/> when the ticket is created.</summary>
    public DateTime DueDate { get; set; }

    /// <summary>Set when the ticket first reaches Resolved or Closed; cleared if it is reopened.</summary>
    public DateTime? ResolvedAt { get; set; }

    public List<TicketComment> Comments { get; set; } = new();

    /// <summary>True when the SLA deadline has passed and the ticket is still unresolved.</summary>
    public bool IsOverdue =>
        ResolvedAt is null &&
        Status is not (TicketStatus.Resolved or TicketStatus.Closed) &&
        DueDate < DateTime.UtcNow;
}
