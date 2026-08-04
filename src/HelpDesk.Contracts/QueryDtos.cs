
namespace HelpDesk.Contracts;

/// <summary>Fields the ticket list can be ordered by.</summary>
public enum TicketSortField
{
    CreatedAt,
    UpdatedAt,
    DueDate,
    Priority,
    Status,
    Title
}

public enum SortDirection
{
    Asc,
    Desc
}

/// <summary>Search, filter, sort and paging options for the ticket list.</summary>
public class TicketQuery
{
    private const int MaxPageSize = 100;
    private int _pageSize = 10;
    private int _page = 1;

    /// <summary>Free-text match against title, description, requester and assignee.</summary>
    public string? Search { get; set; }

    public TicketStatus? Status { get; set; }

    public TicketPriority? Priority { get; set; }

    public TicketCategory? Category { get; set; }

    public string? AssignedTo { get; set; }

    /// <summary>When true, returns only unresolved tickets past their SLA deadline.</summary>
    public bool? OverdueOnly { get; set; }

    public TicketSortField SortBy { get; set; } = TicketSortField.CreatedAt;

    public SortDirection SortDir { get; set; } = SortDirection.Desc;

    public int Page
    {
        get => _page;
        set => _page = value < 1 ? 1 : value;
    }

    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value switch
        {
            < 1 => 1,
            > MaxPageSize => MaxPageSize,
            _ => value
        };
    }
}

/// <summary>One page of results plus the paging metadata a UI needs to render a pager.</summary>
public class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPrevious => Page > 1;
    public bool HasNext => Page < TotalPages;
}

/// <summary>Aggregated numbers powering the dashboard cards and charts.</summary>
public record TicketStatsDto(
    int Total,
    int Open,
    int InProgress,
    int Resolved,
    int Closed,
    int Overdue,
    int Unassigned,
    double? AverageResolutionHours,
    IReadOnlyDictionary<string, int> ByPriority,
    IReadOnlyDictionary<string, int> ByCategory,
    IReadOnlyList<DailyCountDto> CreatedLast7Days);

public record DailyCountDto(DateOnly Date, int Count);
