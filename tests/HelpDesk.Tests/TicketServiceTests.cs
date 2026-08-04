using FluentAssertions;
using HelpDesk.Contracts;
using Xunit;

namespace HelpDesk.Tests;

public class TicketServiceTests : IDisposable
{
    private readonly TestDatabase _db = new();

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task CreateTicket_always_starts_Open()
    {
        var created = await _db.Service.CreateTicketAsync(Sample.Ticket());

        created.Status.Should().Be(TicketStatus.Open);
        created.ResolvedAt.Should().BeNull();
        created.Id.Should().BeGreaterThan(0);
    }

    [Theory]
    [InlineData(TicketPriority.Critical, 4)]
    [InlineData(TicketPriority.High, 24)]
    [InlineData(TicketPriority.Medium, 72)]
    [InlineData(TicketPriority.Low, 168)]
    public async Task CreateTicket_derives_the_SLA_due_date_from_the_priority(TicketPriority priority, int expectedHours)
    {
        var created = await _db.Service.CreateTicketAsync(Sample.Ticket(priority: priority));

        (created.DueDate - created.CreatedAt).Should().Be(TimeSpan.FromHours(expectedHours));
    }

    [Fact]
    public async Task CreateTicket_writes_a_system_comment_recording_the_intake()
    {
        var created = await _db.Service.CreateTicketAsync(Sample.Ticket());

        var detail = await _db.Service.GetTicketAsync(created.Id);

        detail!.Comments.Should().ContainSingle()
            .Which.Should().Match<CommentDto>(c => c.IsSystem && c.Body.Contains("Ticket raised by"));
    }

    [Fact]
    public async Task CreateTicket_trims_whitespace_and_nulls_out_blank_optional_fields()
    {
        var dto = Sample.Ticket(raisedBy: "  Rajrup Roy Chowdhury  ");
        dto.Title = "   Monitor flickers intermittently   ";
        dto.AssignedTo = "   ";

        var created = await _db.Service.CreateTicketAsync(dto);

        created.Title.Should().Be("Monitor flickers intermittently");
        created.RaisedBy.Should().Be("Rajrup Roy Chowdhury");
        created.AssignedTo.Should().BeNull();
    }

    [Fact]
    public async Task ChangeStatus_to_Resolved_stamps_the_resolution_time()
    {
        var created = await _db.Service.CreateTicketAsync(Sample.Ticket());
        _db.Clock.Advance(TimeSpan.FromHours(5));

        var updated = await _db.Service.ChangeStatusAsync(created.Id, new ChangeStatusDto
        {
            Status = TicketStatus.Resolved,
            ChangedBy = "Test Agent"
        });

        updated!.Status.Should().Be(TicketStatus.Resolved);
        updated.ResolvedAt.Should().Be(created.CreatedAt.AddHours(5));
    }

    [Fact]
    public async Task Reopening_a_resolved_ticket_clears_the_resolution_time()
    {
        var created = await _db.Service.CreateTicketAsync(Sample.Ticket());
        await _db.Service.ChangeStatusAsync(created.Id, new ChangeStatusDto { Status = TicketStatus.Resolved });

        var reopened = await _db.Service.ChangeStatusAsync(created.Id, new ChangeStatusDto { Status = TicketStatus.Open });

        reopened!.Status.Should().Be(TicketStatus.Open);
        reopened.ResolvedAt.Should().BeNull();
    }

    [Fact]
    public async Task Resolved_to_Closed_keeps_the_original_resolution_time()
    {
        var created = await _db.Service.CreateTicketAsync(Sample.Ticket());
        _db.Clock.Advance(TimeSpan.FromHours(2));
        var resolved = await _db.Service.ChangeStatusAsync(created.Id, new ChangeStatusDto { Status = TicketStatus.Resolved });

        _db.Clock.Advance(TimeSpan.FromHours(10));
        var closed = await _db.Service.ChangeStatusAsync(created.Id, new ChangeStatusDto { Status = TicketStatus.Closed });

        closed!.ResolvedAt.Should().Be(resolved!.ResolvedAt);
    }

    [Fact]
    public async Task ChangeStatus_records_the_transition_and_any_note_on_the_audit_trail()
    {
        var created = await _db.Service.CreateTicketAsync(Sample.Ticket());

        await _db.Service.ChangeStatusAsync(created.Id, new ChangeStatusDto
        {
            Status = TicketStatus.InProgress,
            ChangedBy = "Priya Patel",
            Note = "Ordered a replacement roller kit."
        });

        var comments = await _db.Service.GetCommentsAsync(created.Id);

        comments!.Should().Contain(c =>
            c.IsSystem &&
            c.Body.Contains("Priya Patel changed status from Open to In Progress") &&
            c.Body.Contains("Ordered a replacement roller kit"));
    }

    [Fact]
    public async Task ChangeStatus_to_the_current_status_is_a_no_op_and_adds_no_audit_noise()
    {
        var created = await _db.Service.CreateTicketAsync(Sample.Ticket());
        var before = (await _db.Service.GetCommentsAsync(created.Id))!.Count;

        await _db.Service.ChangeStatusAsync(created.Id, new ChangeStatusDto { Status = TicketStatus.Open });

        (await _db.Service.GetCommentsAsync(created.Id))!.Count.Should().Be(before);
    }

    [Fact]
    public async Task UpdateTicket_re_derives_the_due_date_when_the_priority_changes()
    {
        var created = await _db.Service.CreateTicketAsync(Sample.Ticket(priority: TicketPriority.Low));

        var updated = await _db.Service.UpdateTicketAsync(
            created.Id,
            Sample.From(created, d => d.Priority = TicketPriority.Critical));

        // Recalculated from the original creation time, not from "now".
        updated!.DueDate.Should().Be(created.CreatedAt.AddHours(4));
    }

    [Fact]
    public async Task UpdateTicket_honours_an_explicit_due_date_over_the_priority_default()
    {
        var created = await _db.Service.CreateTicketAsync(Sample.Ticket(priority: TicketPriority.Low));
        var chosen = new DateTime(2026, 9, 1, 9, 0, 0, DateTimeKind.Utc);

        var updated = await _db.Service.UpdateTicketAsync(created.Id, Sample.From(created, d =>
        {
            d.Priority = TicketPriority.Critical;
            d.DueDate = chosen;
        }));

        updated!.DueDate.Should().Be(chosen);
    }

    [Fact]
    public async Task UpdateTicket_audits_status_priority_and_assignment_changes_separately()
    {
        var created = await _db.Service.CreateTicketAsync(Sample.Ticket());

        await _db.Service.UpdateTicketAsync(created.Id, Sample.From(created, d =>
        {
            d.Status = TicketStatus.InProgress;
            d.Priority = TicketPriority.Critical;
            d.AssignedTo = "Hardware Support";
        }));

        var comments = await _db.Service.GetCommentsAsync(created.Id);

        comments!.Should().Contain(c => c.Body.Contains("changed status from Open to In Progress"));
        comments.Should().Contain(c => c.Body.Contains("changed priority from Medium to Critical"));
        comments.Should().Contain(c => c.Body.Contains("assigned the ticket to Hardware Support"));
    }

    [Fact]
    public async Task UpdateTicket_returns_null_for_a_ticket_that_does_not_exist()
    {
        var result = await _db.Service.UpdateTicketAsync(4242, new UpdateTicketDto
        {
            Title = "Does not matter",
            Description = "Long enough description to pass validation.",
            RaisedBy = "Nobody"
        });

        result.Should().BeNull();
    }

    [Fact]
    public async Task DeleteTicket_cascades_to_the_comment_thread()
    {
        var created = await _db.Service.CreateTicketAsync(Sample.Ticket());
        await _db.Service.AddCommentAsync(created.Id, new CreateCommentDto { Author = "Agent", Body = "Looking into it." });

        (await _db.Service.DeleteTicketAsync(created.Id)).Should().BeTrue();

        (await _db.Service.GetTicketAsync(created.Id)).Should().BeNull();
        _db.Context.Comments.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteTicket_returns_false_when_the_ticket_is_already_gone()
    {
        (await _db.Service.DeleteTicketAsync(9999)).Should().BeFalse();
    }

    [Fact]
    public async Task AddComment_refuses_a_ticket_that_does_not_exist()
    {
        var result = await _db.Service.AddCommentAsync(9999, new CreateCommentDto { Author = "A", Body = "Hello" });

        result.Should().BeNull();
    }

    [Fact]
    public async Task System_comments_cannot_be_deleted()
    {
        var created = await _db.Service.CreateTicketAsync(Sample.Ticket());
        var system = (await _db.Service.GetCommentsAsync(created.Id))!.Single(c => c.IsSystem);

        (await _db.Service.DeleteCommentAsync(created.Id, system.Id)).Should().BeFalse();
        (await _db.Service.GetCommentsAsync(created.Id))!.Should().Contain(c => c.Id == system.Id);
    }

    [Fact]
    public async Task User_comments_can_be_deleted()
    {
        var created = await _db.Service.CreateTicketAsync(Sample.Ticket());
        var comment = await _db.Service.AddCommentAsync(created.Id,
            new CreateCommentDto { Author = "Rajrup", Body = "Any progress on this?" });

        (await _db.Service.DeleteCommentAsync(created.Id, comment!.Id)).Should().BeTrue();
        (await _db.Service.GetCommentsAsync(created.Id))!.Should().NotContain(c => c.Id == comment.Id);
    }

    [Fact]
    public async Task Stats_count_each_status_and_the_overdue_backlog()
    {
        await _db.Service.CreateTicketAsync(Sample.Ticket(priority: TicketPriority.Critical));
        var second = await _db.Service.CreateTicketAsync(Sample.Ticket(priority: TicketPriority.Low));
        await _db.Service.ChangeStatusAsync(second.Id, new ChangeStatusDto { Status = TicketStatus.Resolved });

        // Move past the Critical ticket's 4 hour window but well inside the Low ticket's 7 days.
        _db.Clock.Advance(TimeSpan.FromHours(6));

        var stats = await _db.Service.GetStatsAsync();

        stats.Total.Should().Be(2);
        stats.Open.Should().Be(1);
        stats.Resolved.Should().Be(1);
        stats.Overdue.Should().Be(1);
        stats.Unassigned.Should().Be(2);
        stats.ByPriority["Critical"].Should().Be(1);
        stats.ByPriority["Low"].Should().Be(1);
    }

    [Fact]
    public async Task Stats_average_resolution_hours_ignores_still_open_tickets()
    {
        var first = await _db.Service.CreateTicketAsync(Sample.Ticket());
        await _db.Service.CreateTicketAsync(Sample.Ticket());

        _db.Clock.Advance(TimeSpan.FromHours(3));
        await _db.Service.ChangeStatusAsync(first.Id, new ChangeStatusDto { Status = TicketStatus.Resolved });

        var stats = await _db.Service.GetStatsAsync();

        stats.AverageResolutionHours.Should().Be(3);
    }

    [Fact]
    public async Task Stats_report_a_null_average_when_nothing_has_been_resolved()
    {
        await _db.Service.CreateTicketAsync(Sample.Ticket());

        (await _db.Service.GetStatsAsync()).AverageResolutionHours.Should().BeNull();
    }

    [Fact]
    public async Task Stats_trend_always_covers_exactly_seven_days_ending_today()
    {
        await _db.Service.CreateTicketAsync(Sample.Ticket());

        var stats = await _db.Service.GetStatsAsync();

        stats.CreatedLast7Days.Should().HaveCount(7);
        stats.CreatedLast7Days[^1].Date.Should().Be(new DateOnly(2026, 8, 4));
        stats.CreatedLast7Days[^1].Count.Should().Be(1);
    }
}
