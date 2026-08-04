using HelpDesk.Api.Models;

namespace HelpDesk.Api.Mapping;

/// <summary>Hand-rolled entity to DTO projection — no reflection, no mapper configuration to keep in sync.</summary>
public static class TicketMapper
{
    public static TicketDto ToDto(this Ticket ticket, int? commentCount = null) => new(
        ticket.Id,
        ticket.Title,
        ticket.Description,
        ticket.Priority,
        ticket.Status,
        ticket.Category,
        ticket.RaisedBy,
        ticket.RaisedByEmail,
        ticket.AssignedTo,
        ticket.CreatedAt,
        ticket.UpdatedAt,
        ticket.DueDate,
        ticket.ResolvedAt,
        ticket.IsOverdue,
        commentCount ?? ticket.Comments.Count);

    public static CommentDto ToDto(this TicketComment comment) => new(
        comment.Id,
        comment.TicketId,
        comment.Author,
        comment.Body,
        comment.IsSystem,
        comment.CreatedAt);
}
