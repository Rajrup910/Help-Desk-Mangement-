using System.Globalization;
using HelpDesk.Api.Mapping;
using HelpDesk.Api.Models;
using HelpDesk.Api.Repositories;

namespace HelpDesk.Api.Services;

/// <summary>
/// Owns the ticket business rules: SLA due dates, status transition side effects and the audit trail.
/// Controllers stay thin and the repository stays free of policy.
/// </summary>
public class TicketService : ITicketService
{
    private readonly ITicketRepository _repository;
    private readonly ILogger<TicketService> _logger;
    private readonly TimeProvider _clock;

    public TicketService(ITicketRepository repository, ILogger<TicketService> logger, TimeProvider clock)
    {
        _repository = repository;
        _logger = logger;
        _clock = clock;
    }

    private DateTime UtcNow => _clock.GetUtcNow().UtcDateTime;

    public async Task<PagedResult<TicketDto>> GetTicketsAsync(TicketQuery query, CancellationToken ct = default)
    {
        var (items, totalCount) = await _repository.QueryAsync(query, ct);

        return new PagedResult<TicketDto>
        {
            Items = items.Select(t => t.ToDto()).ToList(),
            Page = query.Page,
            PageSize = query.PageSize,
            TotalCount = totalCount
        };
    }

    public async Task<TicketDetailDto?> GetTicketAsync(int id, CancellationToken ct = default)
    {
        var ticket = await _repository.GetByIdAsync(id, includeComments: true, ct: ct);
        if (ticket is null)
        {
            return null;
        }

        var comments = ticket.Comments
            .OrderBy(c => c.CreatedAt)
            .ThenBy(c => c.Id)
            .Select(c => c.ToDto())
            .ToList();

        return new TicketDetailDto(ticket.ToDto(comments.Count), comments);
    }

    public async Task<TicketDto> CreateTicketAsync(CreateTicketDto dto, CancellationToken ct = default)
    {
        var now = UtcNow;

        var ticket = new Ticket
        {
            Title = dto.Title.Trim(),
            Description = dto.Description.Trim(),
            Priority = dto.Priority,
            // A new ticket is always Open — the client cannot open one in a resolved state.
            Status = TicketStatus.Open,
            Category = dto.Category,
            RaisedBy = dto.RaisedBy.Trim(),
            RaisedByEmail = Normalise(dto.RaisedByEmail),
            AssignedTo = Normalise(dto.AssignedTo),
            CreatedAt = now,
            UpdatedAt = now,
            DueDate = dto.DueDate?.ToUniversalTime() ?? SlaPolicy.DueDateFor(dto.Priority, now)
        };

        // Invariant culture: the audit trail must not pick up the server's local date separators.
        var due = ticket.DueDate.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        ticket.Comments.Add(SystemComment(
            $"Ticket raised by {ticket.RaisedBy} with {ticket.Priority} priority. SLA due {due} UTC.",
            now));

        await _repository.AddAsync(ticket, ct);

        _logger.LogInformation("Ticket {TicketId} created by {RaisedBy} ({Priority})", ticket.Id, ticket.RaisedBy, ticket.Priority);

        return ticket.ToDto();
    }

    public async Task<TicketDto?> UpdateTicketAsync(int id, UpdateTicketDto dto, CancellationToken ct = default)
    {
        var ticket = await _repository.GetByIdAsync(id, includeComments: true, tracked: true, ct: ct);
        if (ticket is null)
        {
            return null;
        }

        var now = UtcNow;
        var actor = string.IsNullOrWhiteSpace(dto.ChangedBy) ? "System" : dto.ChangedBy.Trim();
        var previousStatus = ticket.Status;
        var previousPriority = ticket.Priority;
        var previousAssignee = ticket.AssignedTo;

        ticket.Title = dto.Title.Trim();
        ticket.Description = dto.Description.Trim();
        ticket.Priority = dto.Priority;
        ticket.Category = dto.Category;
        ticket.RaisedBy = dto.RaisedBy.Trim();
        ticket.RaisedByEmail = Normalise(dto.RaisedByEmail);
        ticket.AssignedTo = Normalise(dto.AssignedTo);
        ticket.UpdatedAt = now;

        if (dto.DueDate is { } due)
        {
            ticket.DueDate = due.ToUniversalTime();
        }
        else if (previousPriority != dto.Priority)
        {
            // Priority moved and no explicit deadline was given — re-derive the SLA from the original creation time.
            ticket.DueDate = SlaPolicy.DueDateFor(dto.Priority, ticket.CreatedAt);
        }

        ApplyStatusTransition(ticket, dto.Status, now);

        if (previousStatus != dto.Status)
        {
            ticket.Comments.Add(SystemComment($"{actor} changed status from {Humanise(previousStatus)} to {Humanise(dto.Status)}.", now));
        }

        if (previousPriority != dto.Priority)
        {
            ticket.Comments.Add(SystemComment($"{actor} changed priority from {previousPriority} to {dto.Priority}.", now));
        }

        if (!string.Equals(previousAssignee, ticket.AssignedTo, StringComparison.OrdinalIgnoreCase))
        {
            var target = ticket.AssignedTo ?? "nobody";
            ticket.Comments.Add(SystemComment($"{actor} assigned the ticket to {target}.", now));
        }

        await _repository.SaveChangesAsync(ct);

        _logger.LogInformation("Ticket {TicketId} updated by {Actor}", ticket.Id, actor);

        return ticket.ToDto();
    }

    public async Task<TicketDto?> ChangeStatusAsync(int id, ChangeStatusDto dto, CancellationToken ct = default)
    {
        var ticket = await _repository.GetByIdAsync(id, includeComments: true, tracked: true, ct: ct);
        if (ticket is null)
        {
            return null;
        }

        var now = UtcNow;
        var actor = string.IsNullOrWhiteSpace(dto.ChangedBy) ? "System" : dto.ChangedBy.Trim();
        var previousStatus = ticket.Status;

        if (previousStatus == dto.Status)
        {
            return ticket.ToDto();
        }

        ApplyStatusTransition(ticket, dto.Status, now);
        ticket.UpdatedAt = now;

        var message = $"{actor} changed status from {Humanise(previousStatus)} to {Humanise(dto.Status)}.";
        if (!string.IsNullOrWhiteSpace(dto.Note))
        {
            message += $" Note: {dto.Note.Trim()}";
        }

        ticket.Comments.Add(SystemComment(message, now));

        await _repository.SaveChangesAsync(ct);

        return ticket.ToDto();
    }

    public Task<bool> DeleteTicketAsync(int id, CancellationToken ct = default) => _repository.DeleteAsync(id, ct);

    public async Task<TicketStatsDto> GetStatsAsync(CancellationToken ct = default)
    {
        var tickets = await _repository.GetAllAsync(ct);
        var now = UtcNow;

        var resolutionTimes = tickets
            .Where(t => t.ResolvedAt is not null)
            .Select(t => (t.ResolvedAt!.Value - t.CreatedAt).TotalHours)
            .Where(hours => hours >= 0)
            .ToList();

        var today = DateOnly.FromDateTime(now);
        var last7Days = Enumerable.Range(0, 7)
            .Select(offset => today.AddDays(-6 + offset))
            .Select(date => new DailyCountDto(
                date,
                tickets.Count(t => DateOnly.FromDateTime(t.CreatedAt) == date)))
            .ToList();

        return new TicketStatsDto(
            Total: tickets.Count,
            Open: tickets.Count(t => t.Status == TicketStatus.Open),
            InProgress: tickets.Count(t => t.Status == TicketStatus.InProgress),
            Resolved: tickets.Count(t => t.Status == TicketStatus.Resolved),
            Closed: tickets.Count(t => t.Status == TicketStatus.Closed),
            Overdue: tickets.Count(t =>
                t.ResolvedAt is null &&
                t.Status is not (TicketStatus.Resolved or TicketStatus.Closed) &&
                t.DueDate < now),
            Unassigned: tickets.Count(t => string.IsNullOrWhiteSpace(t.AssignedTo)),
            AverageResolutionHours: resolutionTimes.Count == 0 ? null : Math.Round(resolutionTimes.Average(), 1),
            ByPriority: Enum.GetValues<TicketPriority>()
                .ToDictionary(p => p.ToString(), p => tickets.Count(t => t.Priority == p)),
            ByCategory: Enum.GetValues<TicketCategory>()
                .ToDictionary(c => c.ToString(), c => tickets.Count(t => t.Category == c)),
            CreatedLast7Days: last7Days);
    }

    public async Task<IReadOnlyList<CommentDto>?> GetCommentsAsync(int ticketId, CancellationToken ct = default)
    {
        if (!await _repository.ExistsAsync(ticketId, ct))
        {
            return null;
        }

        var comments = await _repository.GetCommentsAsync(ticketId, ct);
        return comments.Select(c => c.ToDto()).ToList();
    }

    public async Task<CommentDto?> AddCommentAsync(int ticketId, CreateCommentDto dto, CancellationToken ct = default)
    {
        var ticket = await _repository.GetByIdAsync(ticketId, tracked: true, ct: ct);
        if (ticket is null)
        {
            return null;
        }

        var now = UtcNow;
        var comment = new TicketComment
        {
            TicketId = ticketId,
            Author = dto.Author.Trim(),
            Body = dto.Body.Trim(),
            IsSystem = false,
            CreatedAt = now
        };

        // A new comment counts as activity on the ticket.
        ticket.UpdatedAt = now;

        await _repository.AddCommentAsync(comment, ct);
        await _repository.SaveChangesAsync(ct);

        return comment.ToDto();
    }

    public async Task<bool> DeleteCommentAsync(int ticketId, int commentId, CancellationToken ct = default)
    {
        var comment = await _repository.GetCommentAsync(ticketId, commentId, ct);
        if (comment is null || comment.IsSystem)
        {
            // System comments are the audit trail and are not user-deletable.
            return false;
        }

        return await _repository.DeleteCommentAsync(ticketId, commentId, ct);
    }

    /// <summary>
    /// Moving to Resolved/Closed stamps the resolution time; reopening clears it so the
    /// average-resolution metric never counts a ticket that is open again.
    /// </summary>
    private static void ApplyStatusTransition(Ticket ticket, TicketStatus newStatus, DateTime now)
    {
        var wasResolvedState = ticket.Status is TicketStatus.Resolved or TicketStatus.Closed;
        var isResolvedState = newStatus is TicketStatus.Resolved or TicketStatus.Closed;

        ticket.Status = newStatus;

        if (isResolvedState && !wasResolvedState)
        {
            ticket.ResolvedAt = now;
        }
        else if (!isResolvedState)
        {
            ticket.ResolvedAt = null;
        }
    }

    private static TicketComment SystemComment(string body, DateTime now) => new()
    {
        Author = "Help Desk",
        Body = body,
        IsSystem = true,
        CreatedAt = now
    };

    private static string Humanise(TicketStatus status) =>
        status == TicketStatus.InProgress ? "In Progress" : status.ToString();

    private static string? Normalise(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
