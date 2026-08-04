using HelpDesk.Api.Data;
using HelpDesk.Api.Repositories;
using HelpDesk.Api.Services;
using HelpDesk.Contracts;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace HelpDesk.Tests;

/// <summary>
/// Spins up a real SQLite database in memory for each test. Using the actual provider rather than
/// the EF in-memory provider means the LINQ translations, the string-stored enums and the UTC
/// value converters are all genuinely exercised.
/// </summary>
public sealed class TestDatabase : IDisposable
{
    private readonly SqliteConnection _connection;

    public TestDatabase()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<HelpDeskDbContext>()
            .UseSqlite(_connection)
            .Options;

        Context = new HelpDeskDbContext(options);
        Context.Database.EnsureCreated();

        Repository = new TicketRepository(Context);
        Clock = new FakeTimeProvider(new DateTime(2026, 8, 4, 12, 0, 0, DateTimeKind.Utc));
        Service = new TicketService(Repository, NullLogger<TicketService>.Instance, Clock);
    }

    public HelpDeskDbContext Context { get; }

    public ITicketRepository Repository { get; }

    public ITicketService Service { get; }

    public FakeTimeProvider Clock { get; }

    public void Dispose()
    {
        Context.Dispose();
        _connection.Dispose();
    }
}

/// <summary>A clock the tests can move, so SLA and resolution-time assertions are deterministic.</summary>
public sealed class FakeTimeProvider : TimeProvider
{
    private DateTimeOffset _now;

    public FakeTimeProvider(DateTime utcNow) => _now = new DateTimeOffset(utcNow);

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan by) => _now = _now.Add(by);
}

public static class Sample
{
    public static CreateTicketDto Ticket(
        string title = "Printer jams on every multi-page job",
        string description = "The shared printer on the third floor jams whenever a job is longer than two pages.",
        TicketPriority priority = TicketPriority.Medium,
        TicketCategory category = TicketCategory.Hardware,
        string raisedBy = "Rajrup Roy Chowdhury",
        string? assignedTo = null) => new()
    {
        Title = title,
        Description = description,
        Priority = priority,
        Category = category,
        RaisedBy = raisedBy,
        AssignedTo = assignedTo
    };

    public static UpdateTicketDto From(TicketDto ticket, Action<UpdateTicketDto>? mutate = null)
    {
        var dto = new UpdateTicketDto
        {
            Title = ticket.Title,
            Description = ticket.Description,
            Priority = ticket.Priority,
            Status = ticket.Status,
            Category = ticket.Category,
            RaisedBy = ticket.RaisedBy,
            RaisedByEmail = ticket.RaisedByEmail,
            AssignedTo = ticket.AssignedTo,
            ChangedBy = "Test Agent"
        };

        mutate?.Invoke(dto);
        return dto;
    }
}
