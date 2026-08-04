using FluentAssertions;
using HelpDesk.Api.Models;
using HelpDesk.Contracts;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HelpDesk.Tests;

public class TicketRepositoryTests : IDisposable
{
    private readonly TestDatabase _db = new();

    public void Dispose() => _db.Dispose();

    private async Task SeedAsync()
    {
        var baseTime = new DateTime(2026, 8, 1, 9, 0, 0, DateTimeKind.Utc);

        var tickets = new[]
        {
            New("VPN keeps dropping", TicketPriority.Critical, TicketStatus.Open, TicketCategory.Network, "Rahul Sharma", "Network Team", baseTime),
            New("Need a Visual Studio licence", TicketPriority.Medium, TicketStatus.Open, TicketCategory.Software, "Priya Patel", null, baseTime.AddHours(2)),
            New("Monitor flickers on the dock", TicketPriority.Low, TicketStatus.Resolved, TicketCategory.Hardware, "Amit Kumar", "Hardware Support", baseTime.AddHours(4)),
            New("Shared drive will not map", TicketPriority.High, TicketStatus.InProgress, TicketCategory.Access, "Arjun Menon", "Identity Team", baseTime.AddHours(6)),
            New("Duplicate charge on invoice 50% off", TicketPriority.High, TicketStatus.Closed, TicketCategory.Billing, "Vikram Desai", "Finance Ops", baseTime.AddHours(8))
        };

        _db.Context.Tickets.AddRange(tickets);
        await _db.Context.SaveChangesAsync();
    }

    private static Ticket New(
        string title,
        TicketPriority priority,
        TicketStatus status,
        TicketCategory category,
        string raisedBy,
        string? assignedTo,
        DateTime createdAt) => new()
    {
        Title = title,
        Description = $"Description for: {title}. Padded so it clears the minimum length rule.",
        Priority = priority,
        Status = status,
        Category = category,
        RaisedBy = raisedBy,
        AssignedTo = assignedTo,
        CreatedAt = createdAt,
        UpdatedAt = createdAt,
        DueDate = SlaPolicy.DueDateFor(priority, createdAt),
        ResolvedAt = status is TicketStatus.Resolved or TicketStatus.Closed ? createdAt.AddHours(1) : null
    };

    [Fact]
    public async Task Query_returns_every_ticket_when_no_filter_is_applied()
    {
        await SeedAsync();

        var (items, total) = await _db.Repository.QueryAsync(new TicketQuery { PageSize = 100 });

        total.Should().Be(5);
        items.Should().HaveCount(5);
    }

    [Fact]
    public async Task Query_filters_by_status()
    {
        await SeedAsync();

        var (items, total) = await _db.Repository.QueryAsync(new TicketQuery { Status = TicketStatus.Open });

        total.Should().Be(2);
        items.Should().OnlyContain(t => t.Status == TicketStatus.Open);
    }

    [Fact]
    public async Task Query_search_matches_title_description_requester_and_assignee()
    {
        await SeedAsync();

        (await _db.Repository.QueryAsync(new TicketQuery { Search = "VPN" })).TotalCount.Should().Be(1);
        (await _db.Repository.QueryAsync(new TicketQuery { Search = "Priya" })).TotalCount.Should().Be(1);
        (await _db.Repository.QueryAsync(new TicketQuery { Search = "Identity Team" })).TotalCount.Should().Be(1);
        (await _db.Repository.QueryAsync(new TicketQuery { Search = "Description for" })).TotalCount.Should().Be(5);
    }

    [Fact]
    public async Task Query_search_treats_LIKE_wildcards_as_literal_text()
    {
        await SeedAsync();

        // Without escaping, "%" would match every row rather than the one invoice ticket.
        var (items, total) = await _db.Repository.QueryAsync(new TicketQuery { Search = "50%" });

        total.Should().Be(1);
        items.Single().Title.Should().Contain("50%");
    }

    [Fact]
    public async Task Query_can_isolate_the_unassigned_queue()
    {
        await SeedAsync();

        var (items, total) = await _db.Repository.QueryAsync(new TicketQuery { AssignedTo = "unassigned" });

        total.Should().Be(1);
        items.Single().RaisedBy.Should().Be("Priya Patel");
    }

    [Fact]
    public async Task Query_overdue_filter_excludes_resolved_and_closed_tickets()
    {
        await SeedAsync();

        // Every seeded ticket is dated 2026-08-01 and DateTime.UtcNow is well past every SLA,
        // so only the genuinely unresolved ones may come back.
        var (items, _) = await _db.Repository.QueryAsync(new TicketQuery { OverdueOnly = true, PageSize = 100 });

        items.Should().OnlyContain(t => t.Status != TicketStatus.Resolved && t.Status != TicketStatus.Closed);
        items.Should().HaveCount(3);
    }

    [Fact]
    public async Task Query_sorts_by_priority_severity_not_alphabetically()
    {
        await SeedAsync();

        var (items, _) = await _db.Repository.QueryAsync(new TicketQuery
        {
            SortBy = TicketSortField.Priority,
            SortDir = SortDirection.Asc,
            PageSize = 100
        });

        // Alphabetical ordering of the stored text would put "Critical, High, High, Low, Medium".
        items.Select(t => t.Priority).Should().ContainInOrder(
            TicketPriority.Critical,
            TicketPriority.High,
            TicketPriority.High,
            TicketPriority.Medium,
            TicketPriority.Low);
    }

    [Fact]
    public async Task Query_sorts_by_status_in_workflow_order()
    {
        await SeedAsync();

        var (items, _) = await _db.Repository.QueryAsync(new TicketQuery
        {
            SortBy = TicketSortField.Status,
            SortDir = SortDirection.Asc,
            PageSize = 100
        });

        items.Select(t => t.Status).Should().ContainInOrder(
            TicketStatus.Open,
            TicketStatus.Open,
            TicketStatus.InProgress,
            TicketStatus.Resolved,
            TicketStatus.Closed);
    }

    [Fact]
    public async Task Query_pages_without_overlapping_or_dropping_rows()
    {
        await SeedAsync();

        var page1 = await _db.Repository.QueryAsync(new TicketQuery { Page = 1, PageSize = 2 });
        var page2 = await _db.Repository.QueryAsync(new TicketQuery { Page = 2, PageSize = 2 });
        var page3 = await _db.Repository.QueryAsync(new TicketQuery { Page = 3, PageSize = 2 });

        page1.TotalCount.Should().Be(5);
        page1.Items.Should().HaveCount(2);
        page2.Items.Should().HaveCount(2);
        page3.Items.Should().HaveCount(1);

        var ids = page1.Items.Concat(page2.Items).Concat(page3.Items).Select(t => t.Id).ToList();
        ids.Should().OnlyHaveUniqueItems().And.HaveCount(5);
    }

    [Fact]
    public void PageSize_is_clamped_to_a_sane_range()
    {
        new TicketQuery { PageSize = 0 }.PageSize.Should().Be(1);
        new TicketQuery { PageSize = 5000 }.PageSize.Should().Be(100);
        new TicketQuery { Page = -3 }.Page.Should().Be(1);
    }

    [Fact]
    public async Task Timestamps_round_trip_as_UTC()
    {
        await SeedAsync();

        var ticket = await _db.Repository.GetByIdAsync(1);

        // SQLite has no notion of DateTimeKind; the value converter has to re-stamp it.
        ticket!.CreatedAt.Kind.Should().Be(DateTimeKind.Utc);
        ticket.DueDate.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public async Task Enums_are_persisted_as_readable_text()
    {
        await SeedAsync();

        // EF requires the scalar column of a SqlQuery to be named "Value".
        var stored = await _db.Context.Database
            .SqlQueryRaw<string>("SELECT Status AS Value FROM Tickets WHERE Id = 1")
            .ToListAsync();

        stored.Single().Should().Be("Open");
    }
}
