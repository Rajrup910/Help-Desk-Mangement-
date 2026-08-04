using HelpDesk.Contracts;

namespace HelpDesk.Mvc.Models;

public class DashboardViewModel
{
    public TicketStatsDto Stats { get; init; } = null!;

    public IReadOnlyList<TicketDto> RecentTickets { get; init; } = Array.Empty<TicketDto>();

    public IReadOnlyList<TicketDto> BreachingTickets { get; init; } = Array.Empty<TicketDto>();
}

public class TicketListViewModel
{
    public PagedResult<TicketDto> Result { get; init; } = new();

    public TicketQuery Query { get; init; } = new();

    /// <summary>Distinct assignees across the current result set, used to populate the filter dropdown.</summary>
    public IReadOnlyList<string> Assignees { get; init; } = Array.Empty<string>();
}

public class TicketDetailViewModel
{
    public TicketDto Ticket { get; init; } = null!;

    public IReadOnlyList<CommentDto> Comments { get; init; } = Array.Empty<CommentDto>();

    public CreateCommentDto NewComment { get; init; } = new();
}

/// <summary>Wraps the shared update contract so the edit view can bind to it and still show the ticket id.</summary>
public class TicketEditViewModel
{
    public int Id { get; set; }

    public UpdateTicketDto Form { get; set; } = new();

    public DateTime CreatedAt { get; set; }

    public DateTime? ResolvedAt { get; set; }
}

public class ErrorViewModel
{
    public string? RequestId { get; set; }

    public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);

    public string Message { get; set; } = "Something went wrong while processing your request.";
}
