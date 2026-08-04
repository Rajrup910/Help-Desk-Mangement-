using HelpDesk.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace HelpDesk.Api.Data;

/// <summary>
/// Seeds demo data on first run. Kept out of <c>HasData</c> on purpose: the seeded rows use
/// timestamps relative to "now", so the dashboard charts always show a sensible recent window
/// instead of dates frozen at build time.
/// </summary>
public static class DbSeeder
{
    public static async Task SeedAsync(HelpDeskDbContext context, CancellationToken cancellationToken = default)
    {
        if (await context.Tickets.AnyAsync(cancellationToken))
        {
            return;
        }

        var now = DateTime.UtcNow;

        var tickets = new List<Ticket>
        {
            Build(
                title: "VPN drops every few minutes on home Wi-Fi",
                description: "Since the client update on Monday the VPN disconnects roughly every 5 minutes. Reconnecting works but the session is lost. Happens on both laptop and desktop.",
                priority: TicketPriority.Critical,
                status: TicketStatus.InProgress,
                category: TicketCategory.Network,
                raisedBy: "Rahul Sharma",
                email: "rahul.sharma@example.com",
                assignedTo: "Network Team",
                createdAt: now.AddHours(-6),
                comments: new[]
                {
                    ("Network Team", "Reproduced on the 6.2 client. Rolling back to 6.1 on your machine as a workaround.", false)
                }),

            Build(
                title: "Visual Studio Enterprise license key required",
                description: "Starting on the payments migration next week and need an Enterprise license key for profiling and architecture tooling.",
                priority: TicketPriority.Medium,
                status: TicketStatus.Open,
                category: TicketCategory.Software,
                raisedBy: "Priya Patel",
                email: "priya.patel@example.com",
                assignedTo: null,
                createdAt: now.AddDays(-1).AddHours(-2),
                comments: Array.Empty<(string, string, bool)>()),

            Build(
                title: "Secondary monitor flickers through USB-C dock",
                description: "The external monitor flickers roughly once a minute when connected through the docking station. Direct HDMI to the laptop is fine, so it looks like the dock.",
                priority: TicketPriority.Low,
                status: TicketStatus.Resolved,
                category: TicketCategory.Hardware,
                raisedBy: "Amit Kumar",
                email: "amit.kumar@example.com",
                assignedTo: "Hardware Support",
                createdAt: now.AddDays(-5),
                resolvedAt: now.AddDays(-3),
                comments: new[]
                {
                    ("Hardware Support", "Replaced the dock firmware and swapped the USB-C cable. Please confirm after a full day of use.", false),
                    ("Amit Kumar", "No flicker since the swap. Thanks!", false)
                }),

            Build(
                title: "Access request: production analytics dashboard",
                description: "Need read access to the production analytics dashboard to prepare the quarterly reliability report. Manager approval attached in the ticketing email.",
                priority: TicketPriority.High,
                status: TicketStatus.Open,
                category: TicketCategory.Access,
                raisedBy: "Sneha Nair",
                email: "sneha.nair@example.com",
                assignedTo: "Identity Team",
                createdAt: now.AddDays(-2).AddHours(-5),
                comments: Array.Empty<(string, string, bool)>()),

            Build(
                title: "Duplicate charge on the SaaS subscription invoice",
                description: "The August invoice lists the design tool subscription twice. Finance needs a corrected invoice before the month-end close.",
                priority: TicketPriority.High,
                status: TicketStatus.InProgress,
                category: TicketCategory.Billing,
                raisedBy: "Vikram Desai",
                email: "vikram.desai@example.com",
                assignedTo: "Finance Ops",
                createdAt: now.AddDays(-3).AddHours(-1),
                comments: new[]
                {
                    ("Finance Ops", "Raised a correction with the vendor, reference INV-88421. Expecting the credit note in 2 working days.", false)
                }),

            Build(
                title: "Laptop battery drains in under two hours",
                description: "Battery health reports 62% and the machine no longer lasts a meeting block. Requesting a battery replacement or a device swap.",
                priority: TicketPriority.Medium,
                status: TicketStatus.Closed,
                category: TicketCategory.Hardware,
                raisedBy: "Meera Iyer",
                email: "meera.iyer@example.com",
                assignedTo: "Hardware Support",
                createdAt: now.AddDays(-9),
                resolvedAt: now.AddDays(-7),
                comments: new[]
                {
                    ("Hardware Support", "Battery replaced under warranty and health is back to 100%.", false)
                }),

            Build(
                title: "Shared drive mapping fails after password reset",
                description: "After the scheduled password reset the mapped network drive prompts for credentials in a loop and never connects.",
                priority: TicketPriority.High,
                status: TicketStatus.Open,
                category: TicketCategory.Access,
                raisedBy: "Arjun Menon",
                email: "arjun.menon@example.com",
                assignedTo: null,
                // Deliberately past its SLA so the "Overdue" card and filter have something to show.
                createdAt: now.AddDays(-4),
                comments: Array.Empty<(string, string, bool)>()),

            Build(
                title: "Install Node.js LTS and Docker Desktop on new machine",
                description: "New starter setup for the platform team. Needs Node.js LTS, Docker Desktop and the internal certificate bundle installed.",
                priority: TicketPriority.Low,
                status: TicketStatus.InProgress,
                category: TicketCategory.Software,
                raisedBy: "Kavya Reddy",
                email: "kavya.reddy@example.com",
                assignedTo: "Desktop Support",
                createdAt: now.AddHours(-20),
                comments: Array.Empty<(string, string, bool)>())
        };

        context.Tickets.AddRange(tickets);
        await context.SaveChangesAsync(cancellationToken);
    }

    private static Ticket Build(
        string title,
        string description,
        TicketPriority priority,
        TicketStatus status,
        TicketCategory category,
        string raisedBy,
        string? email,
        string? assignedTo,
        DateTime createdAt,
        (string Author, string Body, bool IsSystem)[] comments,
        DateTime? resolvedAt = null)
    {
        var ticket = new Ticket
        {
            Title = title,
            Description = description,
            Priority = priority,
            Status = status,
            Category = category,
            RaisedBy = raisedBy,
            RaisedByEmail = email,
            AssignedTo = assignedTo,
            CreatedAt = createdAt,
            UpdatedAt = resolvedAt ?? createdAt,
            DueDate = createdAt.Add(SlaPolicy.ResolutionWindowFor(priority)),
            ResolvedAt = resolvedAt
        };

        var offset = 1;
        foreach (var (author, body, isSystem) in comments)
        {
            ticket.Comments.Add(new TicketComment
            {
                Author = author,
                Body = body,
                IsSystem = isSystem,
                CreatedAt = createdAt.AddHours(offset++)
            });
        }

        return ticket;
    }
}
