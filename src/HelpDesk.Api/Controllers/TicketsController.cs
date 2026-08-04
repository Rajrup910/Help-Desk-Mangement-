using HelpDesk.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace HelpDesk.Api.Controllers;

/// <summary>CRUD, search and reporting endpoints for support tickets.</summary>
[ApiController]
[Route("api/tickets")]
[Produces("application/json")]
public class TicketsController : ControllerBase
{
    private readonly ITicketService _service;

    public TicketsController(ITicketService service)
    {
        _service = service;
    }

    /// <summary>Returns a filtered, sorted and paged list of tickets.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<TicketDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<TicketDto>>> GetTickets([FromQuery] TicketQuery query, CancellationToken ct)
    {
        var result = await _service.GetTicketsAsync(query, ct);
        return Ok(result);
    }

    /// <summary>Returns a single ticket together with its comment thread.</summary>
    [HttpGet("{id:int}", Name = nameof(GetTicket))]
    [ProducesResponseType(typeof(TicketDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TicketDetailDto>> GetTicket(int id, CancellationToken ct)
    {
        var ticket = await _service.GetTicketAsync(id, ct);
        return ticket is null ? TicketNotFound(id) : Ok(ticket);
    }

    /// <summary>Aggregated counts for the dashboard.</summary>
    [HttpGet("stats")]
    [ProducesResponseType(typeof(TicketStatsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TicketStatsDto>> GetStats(CancellationToken ct) => Ok(await _service.GetStatsAsync(ct));

    /// <summary>Raises a new ticket. The status is always Open and the SLA due date is derived from the priority.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(TicketDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TicketDto>> CreateTicket([FromBody] CreateTicketDto dto, CancellationToken ct)
    {
        var created = await _service.CreateTicketAsync(dto, ct);
        return CreatedAtRoute(nameof(GetTicket), new { id = created.Id }, created);
    }

    /// <summary>Updates every editable field on a ticket. Status, priority and assignee changes are audited.</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(TicketDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TicketDto>> UpdateTicket(int id, [FromBody] UpdateTicketDto dto, CancellationToken ct)
    {
        var updated = await _service.UpdateTicketAsync(id, dto, ct);
        return updated is null ? TicketNotFound(id) : Ok(updated);
    }

    /// <summary>Moves a ticket to a new status without resending the whole record.</summary>
    [HttpPatch("{id:int}/status")]
    [ProducesResponseType(typeof(TicketDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TicketDto>> ChangeStatus(int id, [FromBody] ChangeStatusDto dto, CancellationToken ct)
    {
        var updated = await _service.ChangeStatusAsync(id, dto, ct);
        return updated is null ? TicketNotFound(id) : Ok(updated);
    }

    /// <summary>Permanently deletes a ticket and its comments.</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteTicket(int id, CancellationToken ct)
    {
        var deleted = await _service.DeleteTicketAsync(id, ct);
        return deleted ? NoContent() : TicketNotFound(id);
    }

    /// <summary>Lists the comment thread for a ticket, oldest first.</summary>
    [HttpGet("{id:int}/comments")]
    [ProducesResponseType(typeof(IReadOnlyList<CommentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<CommentDto>>> GetComments(int id, CancellationToken ct)
    {
        var comments = await _service.GetCommentsAsync(id, ct);
        return comments is null ? TicketNotFound(id) : Ok(comments);
    }

    /// <summary>Adds a comment to a ticket.</summary>
    [HttpPost("{id:int}/comments")]
    [ProducesResponseType(typeof(CommentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CommentDto>> AddComment(int id, [FromBody] CreateCommentDto dto, CancellationToken ct)
    {
        var comment = await _service.AddCommentAsync(id, dto, ct);
        return comment is null
            ? TicketNotFound(id)
            : CreatedAtRoute(nameof(GetTicket), new { id }, comment);
    }

    /// <summary>Deletes a user comment. System audit entries cannot be removed.</summary>
    [HttpDelete("{id:int}/comments/{commentId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteComment(int id, int commentId, CancellationToken ct)
    {
        var deleted = await _service.DeleteCommentAsync(id, commentId, ct);
        return deleted
            ? NoContent()
            : Problem(
                title: "Comment not found",
                detail: $"Comment {commentId} does not exist on ticket {id}, or it is a system entry that cannot be deleted.",
                statusCode: StatusCodes.Status404NotFound);
    }

    private ObjectResult TicketNotFound(int id) => Problem(
        title: "Ticket not found",
        detail: $"No ticket exists with ID {id}.",
        statusCode: StatusCodes.Status404NotFound);
}
