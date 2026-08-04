using System.ComponentModel.DataAnnotations;

namespace HelpDesk.Contracts;

/// <summary>Ticket shape returned to clients. Never expose the entity directly.</summary>
public record TicketDto(
    int Id,
    string Title,
    string Description,
    TicketPriority Priority,
    TicketStatus Status,
    TicketCategory Category,
    string RaisedBy,
    string? RaisedByEmail,
    string? AssignedTo,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime DueDate,
    DateTime? ResolvedAt,
    bool IsOverdue,
    int CommentCount);

/// <summary>A ticket plus its full comment thread.</summary>
public record TicketDetailDto(TicketDto Ticket, IReadOnlyList<CommentDto> Comments);

/// <summary>
/// Payload for raising a ticket. Status is deliberately absent — new tickets always start Open,
/// which stops a client from creating an already-closed ticket.
/// </summary>
public class CreateTicketDto
{
    [Required(ErrorMessage = "Title is required.")]
    [StringLength(120, MinimumLength = 5, ErrorMessage = "Title must be between 5 and 120 characters.")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Description is required.")]
    [StringLength(2000, MinimumLength = 10, ErrorMessage = "Description must be between 10 and 2000 characters.")]
    public string Description { get; set; } = string.Empty;

    [EnumDataType(typeof(TicketPriority), ErrorMessage = "Priority must be Low, Medium, High or Critical.")]
    public TicketPriority Priority { get; set; } = TicketPriority.Medium;

    [EnumDataType(typeof(TicketCategory), ErrorMessage = "Category is not a known value.")]
    public TicketCategory Category { get; set; } = TicketCategory.Other;

    [Required(ErrorMessage = "Please tell us who is raising this ticket.")]
    [StringLength(80, MinimumLength = 2)]
    public string RaisedBy { get; set; } = string.Empty;

    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    [StringLength(120)]
    public string? RaisedByEmail { get; set; }

    [StringLength(80)]
    public string? AssignedTo { get; set; }

    /// <summary>Optional override for the SLA deadline. Defaults to the priority-based SLA.</summary>
    public DateTime? DueDate { get; set; }
}

/// <summary>Payload for editing an existing ticket. Status changes are allowed here and audited.</summary>
public class UpdateTicketDto
{
    [Required, StringLength(120, MinimumLength = 5, ErrorMessage = "Title must be between 5 and 120 characters.")]
    public string Title { get; set; } = string.Empty;

    [Required, StringLength(2000, MinimumLength = 10, ErrorMessage = "Description must be between 10 and 2000 characters.")]
    public string Description { get; set; } = string.Empty;

    [EnumDataType(typeof(TicketPriority))]
    public TicketPriority Priority { get; set; }

    [EnumDataType(typeof(TicketStatus))]
    public TicketStatus Status { get; set; }

    [EnumDataType(typeof(TicketCategory))]
    public TicketCategory Category { get; set; }

    [Required, StringLength(80, MinimumLength = 2)]
    public string RaisedBy { get; set; } = string.Empty;

    [EmailAddress, StringLength(120)]
    public string? RaisedByEmail { get; set; }

    [StringLength(80)]
    public string? AssignedTo { get; set; }

    public DateTime? DueDate { get; set; }

    /// <summary>Who performed the edit — recorded on the audit comment when the status moves.</summary>
    [StringLength(80)]
    public string? ChangedBy { get; set; }
}

/// <summary>Payload for the lightweight status-transition endpoint.</summary>
public class ChangeStatusDto
{
    [EnumDataType(typeof(TicketStatus), ErrorMessage = "Status must be Open, InProgress, Resolved or Closed.")]
    public TicketStatus Status { get; set; }

    [StringLength(80)]
    public string? ChangedBy { get; set; }

    [StringLength(500)]
    public string? Note { get; set; }
}

public record CommentDto(int Id, int TicketId, string Author, string Body, bool IsSystem, DateTime CreatedAt);

public class CreateCommentDto
{
    [Required(ErrorMessage = "Author is required.")]
    [StringLength(80, MinimumLength = 2)]
    public string Author { get; set; } = string.Empty;

    [Required(ErrorMessage = "Comment cannot be empty.")]
    [StringLength(1000, MinimumLength = 2)]
    public string Body { get; set; } = string.Empty;
}
