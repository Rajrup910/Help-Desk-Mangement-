using HelpDesk.Api.Models;

namespace HelpDesk.Api.Repositories;

/// <summary>Data access boundary for tickets and their comments.</summary>
public interface ITicketRepository
{
    Task<(IReadOnlyList<Ticket> Items, int TotalCount)> QueryAsync(TicketQuery query, CancellationToken ct = default);

    Task<Ticket?> GetByIdAsync(int id, bool includeComments = false, bool tracked = false, CancellationToken ct = default);

    Task<Ticket> AddAsync(Ticket ticket, CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);

    Task<bool> DeleteAsync(int id, CancellationToken ct = default);

    Task<bool> ExistsAsync(int id, CancellationToken ct = default);

    Task<IReadOnlyList<Ticket>> GetAllAsync(CancellationToken ct = default);

    Task<TicketComment> AddCommentAsync(TicketComment comment, CancellationToken ct = default);

    Task<IReadOnlyList<TicketComment>> GetCommentsAsync(int ticketId, CancellationToken ct = default);

    Task<TicketComment?> GetCommentAsync(int ticketId, int commentId, CancellationToken ct = default);

    Task<bool> DeleteCommentAsync(int ticketId, int commentId, CancellationToken ct = default);
}
