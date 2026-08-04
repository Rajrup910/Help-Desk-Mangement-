using HelpDesk.Contracts;

namespace HelpDesk.Mvc.Services;

/// <summary>Typed client over the Help Desk Web API.</summary>
public interface ITicketApiClient
{
    Task<PagedResult<TicketDto>> GetTicketsAsync(TicketQuery query, CancellationToken ct = default);

    Task<TicketDetailDto?> GetTicketAsync(int id, CancellationToken ct = default);

    Task<TicketStatsDto> GetStatsAsync(CancellationToken ct = default);

    Task<TicketDto> CreateTicketAsync(CreateTicketDto dto, CancellationToken ct = default);

    Task<TicketDto?> UpdateTicketAsync(int id, UpdateTicketDto dto, CancellationToken ct = default);

    Task<TicketDto?> ChangeStatusAsync(int id, ChangeStatusDto dto, CancellationToken ct = default);

    Task<bool> DeleteTicketAsync(int id, CancellationToken ct = default);

    Task<CommentDto?> AddCommentAsync(int ticketId, CreateCommentDto dto, CancellationToken ct = default);

    Task<bool> DeleteCommentAsync(int ticketId, int commentId, CancellationToken ct = default);
}

/// <summary>
/// Raised when the API is unreachable or returns an unexpected status.
/// The reference implementation swallowed these into empty lists, which made a dead API look
/// identical to an empty database — this surfaces the failure instead.
/// </summary>
public class HelpDeskApiException : Exception
{
    public HelpDeskApiException(string message, Exception? inner = null) : base(message, inner)
    {
    }

    /// <summary>Field-level validation errors returned by the API, keyed by property name.</summary>
    public IDictionary<string, string[]> ValidationErrors { get; init; } = new Dictionary<string, string[]>();

    public bool IsValidationFailure => ValidationErrors.Count > 0;
}
