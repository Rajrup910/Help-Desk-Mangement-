using HelpDesk.Api.Data;
using HelpDesk.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace HelpDesk.Api.Repositories;

public class TicketRepository : ITicketRepository
{
    private readonly HelpDeskDbContext _context;

    public TicketRepository(HelpDeskDbContext context)
    {
        _context = context;
    }

    public async Task<(IReadOnlyList<Ticket> Items, int TotalCount)> QueryAsync(TicketQuery query, CancellationToken ct = default)
    {
        var tickets = _context.Tickets.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            // Escape the LIKE wildcards so a user searching for "50%" does not match everything.
            var pattern = $"%{Escape(query.Search.Trim())}%";
            tickets = tickets.Where(t =>
                EF.Functions.Like(t.Title, pattern, EscapeChar) ||
                EF.Functions.Like(t.Description, pattern, EscapeChar) ||
                EF.Functions.Like(t.RaisedBy, pattern, EscapeChar) ||
                (t.AssignedTo != null && EF.Functions.Like(t.AssignedTo, pattern, EscapeChar)));
        }

        if (query.Status is { } status)
        {
            tickets = tickets.Where(t => t.Status == status);
        }

        if (query.Priority is { } priority)
        {
            tickets = tickets.Where(t => t.Priority == priority);
        }

        if (query.Category is { } category)
        {
            tickets = tickets.Where(t => t.Category == category);
        }

        if (!string.IsNullOrWhiteSpace(query.AssignedTo))
        {
            if (string.Equals(query.AssignedTo, "unassigned", StringComparison.OrdinalIgnoreCase))
            {
                tickets = tickets.Where(t => t.AssignedTo == null || t.AssignedTo == string.Empty);
            }
            else
            {
                var assignee = query.AssignedTo.Trim();
                tickets = tickets.Where(t => t.AssignedTo == assignee);
            }
        }

        if (query.OverdueOnly == true)
        {
            var now = DateTime.UtcNow;
            tickets = tickets.Where(t =>
                t.ResolvedAt == null &&
                t.Status != TicketStatus.Resolved &&
                t.Status != TicketStatus.Closed &&
                t.DueDate < now);
        }

        var totalCount = await tickets.CountAsync(ct);

        tickets = ApplySort(tickets, query.SortBy, query.SortDir);

        var items = await tickets
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(t => new Ticket
            {
                Id = t.Id,
                Title = t.Title,
                Description = t.Description,
                Priority = t.Priority,
                Status = t.Status,
                Category = t.Category,
                RaisedBy = t.RaisedBy,
                RaisedByEmail = t.RaisedByEmail,
                AssignedTo = t.AssignedTo,
                CreatedAt = t.CreatedAt,
                UpdatedAt = t.UpdatedAt,
                DueDate = t.DueDate,
                ResolvedAt = t.ResolvedAt,
                // Projected so the list endpoint does not have to load every comment row.
                Comments = t.Comments.Select(c => new TicketComment { Id = c.Id }).ToList()
            })
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task<Ticket?> GetByIdAsync(int id, bool includeComments = false, bool tracked = false, CancellationToken ct = default)
    {
        var tickets = _context.Tickets.AsQueryable();

        if (!tracked)
        {
            tickets = tickets.AsNoTracking();
        }

        if (includeComments)
        {
            tickets = tickets.Include(t => t.Comments.OrderBy(c => c.CreatedAt).ThenBy(c => c.Id));
        }

        return await tickets.FirstOrDefaultAsync(t => t.Id == id, ct);
    }

    public async Task<Ticket> AddAsync(Ticket ticket, CancellationToken ct = default)
    {
        await _context.Tickets.AddAsync(ticket, ct);
        await _context.SaveChangesAsync(ct);
        return ticket;
    }

    public Task SaveChangesAsync(CancellationToken ct = default) => _context.SaveChangesAsync(ct);

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        var ticket = await _context.Tickets.FindAsync(new object?[] { id }, ct);
        if (ticket is null)
        {
            return false;
        }

        _context.Tickets.Remove(ticket);
        await _context.SaveChangesAsync(ct);
        return true;
    }

    public Task<bool> ExistsAsync(int id, CancellationToken ct = default) =>
        _context.Tickets.AnyAsync(t => t.Id == id, ct);

    public async Task<IReadOnlyList<Ticket>> GetAllAsync(CancellationToken ct = default) =>
        await _context.Tickets.AsNoTracking().ToListAsync(ct);

    public async Task<TicketComment> AddCommentAsync(TicketComment comment, CancellationToken ct = default)
    {
        await _context.Comments.AddAsync(comment, ct);
        await _context.SaveChangesAsync(ct);
        return comment;
    }

    public async Task<IReadOnlyList<TicketComment>> GetCommentsAsync(int ticketId, CancellationToken ct = default) =>
        await _context.Comments
            .AsNoTracking()
            .Where(c => c.TicketId == ticketId)
            .OrderBy(c => c.CreatedAt)
            .ThenBy(c => c.Id)
            .ToListAsync(ct);

    public Task<TicketComment?> GetCommentAsync(int ticketId, int commentId, CancellationToken ct = default) =>
        _context.Comments
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.TicketId == ticketId && c.Id == commentId, ct);

    public async Task<bool> DeleteCommentAsync(int ticketId, int commentId, CancellationToken ct = default)
    {
        var comment = await _context.Comments
            .FirstOrDefaultAsync(c => c.TicketId == ticketId && c.Id == commentId, ct);

        if (comment is null)
        {
            return false;
        }

        _context.Comments.Remove(comment);
        await _context.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>
    /// Priority and status are persisted as text, so ordering on the column directly would sort
    /// alphabetically ("Critical" before "Low"). These map to a SQL CASE expression instead, which
    /// keeps the severity order meaningful and still runs in the database.
    /// </summary>
    private static IQueryable<Ticket> ApplySort(IQueryable<Ticket> tickets, TicketSortField sortBy, SortDirection direction)
    {
        var ascending = direction == SortDirection.Asc;

        return sortBy switch
        {
            TicketSortField.Title => Order(tickets, t => t.Title, ascending),
            TicketSortField.UpdatedAt => Order(tickets, t => t.UpdatedAt, ascending),
            TicketSortField.DueDate => Order(tickets, t => t.DueDate, ascending),
            TicketSortField.Priority => Order(tickets, t => t.Priority == TicketPriority.Critical ? 0
                                                          : t.Priority == TicketPriority.High ? 1
                                                          : t.Priority == TicketPriority.Medium ? 2
                                                          : 3, ascending),
            TicketSortField.Status => Order(tickets, t => t.Status == TicketStatus.Open ? 0
                                                        : t.Status == TicketStatus.InProgress ? 1
                                                        : t.Status == TicketStatus.Resolved ? 2
                                                        : 3, ascending),
            _ => Order(tickets, t => t.CreatedAt, ascending)
        };
    }

    private static IQueryable<Ticket> Order<TKey>(
        IQueryable<Ticket> tickets,
        System.Linq.Expressions.Expression<Func<Ticket, TKey>> selector,
        bool ascending) =>
        // ThenBy(Id) guarantees a stable page boundary when the sort key ties.
        ascending
            ? tickets.OrderBy(selector).ThenBy(t => t.Id)
            : tickets.OrderByDescending(selector).ThenBy(t => t.Id);

    private const string EscapeChar = "\\";

    private static string Escape(string term) =>
        term.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
}
