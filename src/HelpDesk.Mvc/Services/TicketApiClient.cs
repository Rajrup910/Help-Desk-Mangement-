using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using HelpDesk.Contracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace HelpDesk.Mvc.Services;

public class TicketApiClient : ITicketApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    private readonly HttpClient _http;
    private readonly ILogger<TicketApiClient> _logger;

    public TicketApiClient(HttpClient http, ILogger<TicketApiClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<PagedResult<TicketDto>> GetTicketsAsync(TicketQuery query, CancellationToken ct = default)
    {
        var url = QueryHelpers.AddQueryString("api/tickets", BuildQueryParameters(query));
        var result = await SendAsync<PagedResult<TicketDto>>(HttpMethod.Get, url, content: null, ct);
        return result ?? new PagedResult<TicketDto> { Page = query.Page, PageSize = query.PageSize };
    }

    public Task<TicketDetailDto?> GetTicketAsync(int id, CancellationToken ct = default) =>
        SendAsync<TicketDetailDto>(HttpMethod.Get, $"api/tickets/{id}", content: null, ct);

    public async Task<TicketStatsDto> GetStatsAsync(CancellationToken ct = default)
    {
        var stats = await SendAsync<TicketStatsDto>(HttpMethod.Get, "api/tickets/stats", content: null, ct);
        return stats ?? new TicketStatsDto(0, 0, 0, 0, 0, 0, 0, null,
            new Dictionary<string, int>(), new Dictionary<string, int>(), Array.Empty<DailyCountDto>());
    }

    public async Task<TicketDto> CreateTicketAsync(CreateTicketDto dto, CancellationToken ct = default)
    {
        var created = await SendAsync<TicketDto>(HttpMethod.Post, "api/tickets", dto, ct);
        return created ?? throw new HelpDeskApiException("The API accepted the ticket but returned no content.");
    }

    public Task<TicketDto?> UpdateTicketAsync(int id, UpdateTicketDto dto, CancellationToken ct = default) =>
        SendAsync<TicketDto>(HttpMethod.Put, $"api/tickets/{id}", dto, ct);

    public Task<TicketDto?> ChangeStatusAsync(int id, ChangeStatusDto dto, CancellationToken ct = default) =>
        SendAsync<TicketDto>(HttpMethod.Patch, $"api/tickets/{id}/status", dto, ct);

    public Task<bool> DeleteTicketAsync(int id, CancellationToken ct = default) =>
        SendNoContentAsync(HttpMethod.Delete, $"api/tickets/{id}", ct);

    public Task<CommentDto?> AddCommentAsync(int ticketId, CreateCommentDto dto, CancellationToken ct = default) =>
        SendAsync<CommentDto>(HttpMethod.Post, $"api/tickets/{ticketId}/comments", dto, ct);

    public Task<bool> DeleteCommentAsync(int ticketId, int commentId, CancellationToken ct = default) =>
        SendNoContentAsync(HttpMethod.Delete, $"api/tickets/{ticketId}/comments/{commentId}", ct);

    // -----------------------------------------------------------------------

    /// <summary>Returns null for 404; throws <see cref="HelpDeskApiException"/> for anything else that is not a success.</summary>
    private async Task<T?> SendAsync<T>(HttpMethod method, string url, object? content, CancellationToken ct)
    {
        using var response = await SendCoreAsync(method, url, content, ct);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return default;
        }

        await EnsureSuccessAsync(response, ct);

        if (response.StatusCode == HttpStatusCode.NoContent ||
            response.Content.Headers.ContentLength is 0)
        {
            return default;
        }

        try
        {
            return await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct);
        }
        catch (JsonException ex)
        {
            throw new HelpDeskApiException("The API returned a response that could not be read.", ex);
        }
    }

    private async Task<bool> SendNoContentAsync(HttpMethod method, string url, CancellationToken ct)
    {
        using var response = await SendCoreAsync(method, url, content: null, ct);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }

        await EnsureSuccessAsync(response, ct);
        return true;
    }

    private async Task<HttpResponseMessage> SendCoreAsync(HttpMethod method, string url, object? content, CancellationToken ct)
    {
        var request = new HttpRequestMessage(method, url);
        if (content is not null)
        {
            request.Content = JsonContent.Create(content, options: JsonOptions);
        }

        try
        {
            return await _http.SendAsync(request, ct);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogError(ex, "Timed out calling {Method} {Url}", method, url);
            throw new HelpDeskApiException(
                $"The Help Desk API did not respond in time. Confirm it is running at {_http.BaseAddress}.", ex);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Could not reach the API at {BaseAddress}{Url}", _http.BaseAddress, url);
            throw new HelpDeskApiException(
                $"Could not reach the Help Desk API at {_http.BaseAddress}. Start the HelpDesk.Api project and try again.", ex);
        }
    }

    /// <summary>Translates an API error body into an exception, keeping any field-level validation detail.</summary>
    private async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(ct);

        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            try
            {
                var problem = JsonSerializer.Deserialize<ValidationProblemDetails>(body, JsonOptions);
                if (problem?.Errors is { Count: > 0 })
                {
                    throw new HelpDeskApiException(problem.Title ?? "The submitted data was rejected.")
                    {
                        ValidationErrors = problem.Errors
                    };
                }
            }
            catch (JsonException)
            {
                // Not a ValidationProblemDetails payload — fall through to the generic message.
            }
        }

        _logger.LogWarning("API returned {StatusCode} for {Url}: {Body}",
            (int)response.StatusCode, response.RequestMessage?.RequestUri, body);

        throw new HelpDeskApiException($"The Help Desk API returned {(int)response.StatusCode} ({response.ReasonPhrase}).");
    }

    private static Dictionary<string, string?> BuildQueryParameters(TicketQuery query)
    {
        var parameters = new Dictionary<string, string?>
        {
            ["page"] = query.Page.ToString(CultureInfo.InvariantCulture),
            ["pageSize"] = query.PageSize.ToString(CultureInfo.InvariantCulture),
            ["sortBy"] = query.SortBy.ToString(),
            ["sortDir"] = query.SortDir.ToString()
        };

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            parameters["search"] = query.Search.Trim();
        }

        if (query.Status is { } status)
        {
            parameters["status"] = status.ToString();
        }

        if (query.Priority is { } priority)
        {
            parameters["priority"] = priority.ToString();
        }

        if (query.Category is { } category)
        {
            parameters["category"] = category.ToString();
        }

        if (!string.IsNullOrWhiteSpace(query.AssignedTo))
        {
            parameters["assignedTo"] = query.AssignedTo.Trim();
        }

        if (query.OverdueOnly == true)
        {
            parameters["overdueOnly"] = "true";
        }

        return parameters;
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
