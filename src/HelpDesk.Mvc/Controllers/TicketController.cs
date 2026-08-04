using HelpDesk.Contracts;
using HelpDesk.Mvc.Models;
using HelpDesk.Mvc.Services;
using Microsoft.AspNetCore.Mvc;

namespace HelpDesk.Mvc.Controllers;

public class TicketController : Controller
{
    private readonly ITicketApiClient _api;
    private readonly ILogger<TicketController> _logger;

    public TicketController(ITicketApiClient api, ILogger<TicketController> logger)
    {
        _api = api;
        _logger = logger;
    }

    // GET: /Ticket/Dashboard
    public async Task<IActionResult> Dashboard(CancellationToken ct)
    {
        try
        {
            var stats = await _api.GetStatsAsync(ct);

            var recent = await _api.GetTicketsAsync(new TicketQuery
            {
                SortBy = TicketSortField.CreatedAt,
                SortDir = SortDirection.Desc,
                PageSize = 6
            }, ct);

            var breaching = await _api.GetTicketsAsync(new TicketQuery
            {
                OverdueOnly = true,
                SortBy = TicketSortField.DueDate,
                SortDir = SortDirection.Asc,
                PageSize = 5
            }, ct);

            return View(new DashboardViewModel
            {
                Stats = stats,
                RecentTickets = recent.Items,
                BreachingTickets = breaching.Items
            });
        }
        catch (HelpDeskApiException ex)
        {
            return ApiUnavailable(ex);
        }
    }

    // GET: /Ticket or /Ticket/Index
    public async Task<IActionResult> Index([FromQuery] TicketQuery query, CancellationToken ct)
    {
        try
        {
            var result = await _api.GetTicketsAsync(query, ct);

            return View(new TicketListViewModel
            {
                Result = result,
                Query = query,
                Assignees = result.Items
                    .Select(t => t.AssignedTo)
                    .Where(a => !string.IsNullOrWhiteSpace(a))
                    .Select(a => a!)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(a => a)
                    .ToList()
            });
        }
        catch (HelpDeskApiException ex)
        {
            return ApiUnavailable(ex);
        }
    }

    // GET: /Ticket/Details/5
    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        try
        {
            var detail = await _api.GetTicketAsync(id, ct);
            if (detail is null)
            {
                return TicketNotFound(id);
            }

            return View(new TicketDetailViewModel
            {
                Ticket = detail.Ticket,
                Comments = detail.Comments,
                NewComment = new CreateCommentDto()
            });
        }
        catch (HelpDeskApiException ex)
        {
            return ApiUnavailable(ex);
        }
    }

    // GET: /Ticket/Create
    public IActionResult Create() => View(new CreateTicketDto());

    // POST: /Ticket/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateTicketDto form, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return View(form);
        }

        try
        {
            var created = await _api.CreateTicketAsync(form, ct);
            TempData["Success"] = $"Ticket #{created.Id} raised successfully.";
            return RedirectToAction(nameof(Details), new { id = created.Id });
        }
        catch (HelpDeskApiException ex)
        {
            AddApiErrorsToModelState(ex);
            return View(form);
        }
    }

    // GET: /Ticket/Edit/5
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        try
        {
            var detail = await _api.GetTicketAsync(id, ct);
            if (detail is null)
            {
                return TicketNotFound(id);
            }

            var t = detail.Ticket;
            return View(new TicketEditViewModel
            {
                Id = t.Id,
                CreatedAt = t.CreatedAt,
                ResolvedAt = t.ResolvedAt,
                Form = new UpdateTicketDto
                {
                    Title = t.Title,
                    Description = t.Description,
                    Priority = t.Priority,
                    Status = t.Status,
                    Category = t.Category,
                    RaisedBy = t.RaisedBy,
                    RaisedByEmail = t.RaisedByEmail,
                    AssignedTo = t.AssignedTo,
                    DueDate = t.DueDate
                }
            });
        }
        catch (HelpDeskApiException ex)
        {
            return ApiUnavailable(ex);
        }
    }

    // POST: /Ticket/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, TicketEditViewModel model, CancellationToken ct)
    {
        model.Id = id;

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            var updated = await _api.UpdateTicketAsync(id, model.Form, ct);
            if (updated is null)
            {
                return TicketNotFound(id);
            }

            TempData["Success"] = $"Ticket #{id} updated.";
            return RedirectToAction(nameof(Details), new { id });
        }
        catch (HelpDeskApiException ex)
        {
            AddApiErrorsToModelState(ex);
            return View(model);
        }
    }

    // POST: /Ticket/ChangeStatus/5 — quick transition from the details page.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeStatus(int id, TicketStatus status, string? changedBy, CancellationToken ct)
    {
        try
        {
            var updated = await _api.ChangeStatusAsync(id, new ChangeStatusDto
            {
                Status = status,
                ChangedBy = string.IsNullOrWhiteSpace(changedBy) ? "Help Desk Agent" : changedBy
            }, ct);

            if (updated is null)
            {
                return TicketNotFound(id);
            }

            TempData["Success"] = $"Ticket #{id} moved to {Display.Label(status)}.";
        }
        catch (HelpDeskApiException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    // POST: /Ticket/AddComment/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddComment(int id, CreateCommentDto newComment, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "A comment needs both an author and a message.";
            return RedirectToAction(nameof(Details), new { id });
        }

        try
        {
            var created = await _api.AddCommentAsync(id, newComment, ct);
            if (created is null)
            {
                return TicketNotFound(id);
            }

            TempData["Success"] = "Comment added.";
        }
        catch (HelpDeskApiException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    // POST: /Ticket/DeleteComment
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteComment(int id, int commentId, CancellationToken ct)
    {
        try
        {
            TempData[await _api.DeleteCommentAsync(id, commentId, ct) ? "Success" : "Error"] =
                "Comment removed.";
        }
        catch (HelpDeskApiException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    // GET: /Ticket/Delete/5
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        try
        {
            var detail = await _api.GetTicketAsync(id, ct);
            return detail is null ? TicketNotFound(id) : View(detail.Ticket);
        }
        catch (HelpDeskApiException ex)
        {
            return ApiUnavailable(ex);
        }
    }

    // POST: /Ticket/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id, CancellationToken ct)
    {
        try
        {
            if (await _api.DeleteTicketAsync(id, ct))
            {
                TempData["Success"] = $"Ticket #{id} was deleted.";
            }
            else
            {
                TempData["Error"] = $"Ticket #{id} no longer exists.";
            }
        }
        catch (HelpDeskApiException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    // -----------------------------------------------------------------------

    /// <summary>Surfaces the API's field-level errors against the matching form inputs.</summary>
    private void AddApiErrorsToModelState(HelpDeskApiException ex)
    {
        if (!ex.IsValidationFailure)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return;
        }

        foreach (var (field, messages) in ex.ValidationErrors)
        {
            foreach (var message in messages)
            {
                // The API validates the DTO directly; the edit form nests it under "Form".
                var key = ModelState.ContainsKey(field) ? field : $"Form.{field}";
                ModelState.AddModelError(ModelState.ContainsKey(key) ? key : string.Empty, message);
            }
        }
    }

    private IActionResult TicketNotFound(int id)
    {
        Response.StatusCode = StatusCodes.Status404NotFound;
        return View("ApiError", new ErrorViewModel
        {
            Message = $"Ticket #{id} could not be found. It may have been deleted."
        });
    }

    private IActionResult ApiUnavailable(HelpDeskApiException ex)
    {
        _logger.LogError(ex, "Help Desk API call failed");
        Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        return View("ApiError", new ErrorViewModel { Message = ex.Message });
    }
}
