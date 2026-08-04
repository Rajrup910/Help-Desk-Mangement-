
namespace HelpDesk.Api.Services;

public interface ITicketService
{
    Task<PagedResult<TicketDto>> GetTicketsAsync(TicketQuery query, CancellationToken ct = default);

    Task<TicketDetailDto?> GetTicketAsync(int id, CancellationToken ct = default);

    Task<TicketDto> CreateTicketAsync(CreateTicketDto dto, CancellationToken ct = default);

    Task<TicketDto?> UpdateTicketAsync(int id, UpdateTicketDto dto, CancellationToken ct = default);

    Task<TicketDto?> ChangeStatusAsync(int id, ChangeStatusDto dto, CancellationToken ct = default);

    Task<bool> DeleteTicketAsync(int id, CancellationToken ct = default);

    Task<TicketStatsDto> GetStatsAsync(CancellationToken ct = default);

    Task<IReadOnlyList<CommentDto>?> GetCommentsAsync(int ticketId, CancellationToken ct = default);

    Task<CommentDto?> AddCommentAsync(int ticketId, CreateCommentDto dto, CancellationToken ct = default);

    Task<bool> DeleteCommentAsync(int ticketId, int commentId, CancellationToken ct = default);
}
