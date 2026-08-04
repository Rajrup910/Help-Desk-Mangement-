# Help Desk Management System (`HelpDeskManagement_IN26010404`)
## Project Submission Report & Code Documentation

**Name:** Rajrup Roy Chowdhury
**Registration Number:** IN26010404
**Submission Date:** _______________
**Repository:** _______________

---

### 1. Executive Summary & Project Objective

The objective of this project was to build an internal Help Desk system that lets employees raise
support tickets and lets help desk agents track, assign, update and close them.

The solution is built as two ASP.NET Core applications. The **Web API** handles all the data and the
business rules, and the **MVC web application** provides the user interface. The MVC application does
not connect to the database at all — it calls the Web API over HTTP for everything. This keeps the
two layers separate, so the same API could later be used by a mobile app or another client.

The main features delivered are:

- Create, view, edit and delete support tickets (full CRUD)
- Search, filter by status / priority / category, sort by column and page through results
- SLA due dates that are calculated automatically from the ticket priority
- Comments on each ticket, plus an automatic history of every status, priority and assignment change
- A dashboard showing summary counts, category and priority breakdowns and overdue tickets
- Validation on both the client side and the server side

---

### 2. System Architecture & Technical Stack

```
  Browser  ──►  HelpDesk.Mvc  ──HTTP/JSON──►  HelpDesk.Api  ──►  SQLite Database
                (Razor Views)                 (REST + EF Core)
                      │                             │
                      └────────────┬────────────────┘
                                   ▼
                          HelpDesk.Contracts
                     (Enums, DTOs, Validation Rules)
```

| Layer | Technology Used |
| :--- | :--- |
| Backend Framework | ASP.NET Core 8.0 Web API |
| Frontend Framework | ASP.NET Core 8.0 MVC (Razor Views) |
| Database | SQLite (default) — can be switched to SQL Server through configuration |
| ORM | Entity Framework Core 8.0 |
| API Documentation | Swagger / OpenAPI (Swashbuckle) |
| Unit Testing | xUnit with FluentAssertions |
| Styling | Custom CSS (no framework) |

**Project structure**

| Project | Responsibility |
| :--- | :--- |
| `src/HelpDesk.Contracts` | Shared enums, DTOs and validation attributes used by both applications |
| `src/HelpDesk.Api` | Controllers → Services (business rules) → Repositories (data access) |
| `src/HelpDesk.Mvc` | Controllers → API client → Razor views |
| `tests/HelpDesk.Tests` | Unit tests for the service and repository layers |

The **Repository Pattern** was used to separate the database code from the business logic, and a
**Service layer** was added on top so that the controllers stay small and only handle HTTP concerns.

---

### 3. Application Screenshots & Visual Proof

> **How to complete this section:** take each screenshot listed below, save it into the
> `screenshots/` folder using the exact file name shown, and the image will appear automatically.
> Delete this instruction block before submitting.

**Figure 1: Web API running with Swagger documentation**
*Capture `http://localhost:5285/swagger` showing the list of endpoints.*

![Figure 1: Web API Swagger Documentation](screenshots/01-api-swagger.png)

<br>

**Figure 2: Help Desk Dashboard**
*Capture `http://localhost:5185` showing the summary cards and bar charts.*

![Figure 2: Help Desk Dashboard](screenshots/02-dashboard.png)

<br>

**Figure 3: All Tickets list**
*Capture `http://localhost:5185/Ticket/Index` showing the full ticket table.*

![Figure 3: Support Tickets List](screenshots/03-ticket-list.png)

<br>

**Figure 4: Search and filter working**
*Apply a status or priority filter, then capture the filtered result.*

![Figure 4: Filtered Ticket List](screenshots/04-ticket-filter.png)

<br>

**Figure 5: Ticket details with comment history**
*Capture `http://localhost:5185/Ticket/Details/5` showing the comments and status buttons.*

![Figure 5: Ticket Details Page](screenshots/05-ticket-details.png)

<br>

**Figure 6: Raise a new ticket form**
*Capture `http://localhost:5185/Ticket/Create`.*

![Figure 6: New Ticket Form](screenshots/06-ticket-create.png)

<br>

**Figure 7: Validation error messages**
*Submit the create form with empty fields and capture the red error messages.*

![Figure 7: Form Validation Errors](screenshots/07-validation.png)

<br>

**Figure 8: Edit ticket form**
*Capture `http://localhost:5185/Ticket/Edit/2`.*

![Figure 8: Edit Ticket Form](screenshots/08-ticket-edit.png)

<br>

**Figure 9: Delete confirmation page**
*Capture `http://localhost:5185/Ticket/Delete/3`.*

![Figure 9: Delete Confirmation Page](screenshots/09-ticket-delete.png)

<br>

**Figure 10: Database records**
*Optional — open `helpdesk.db` in DB Browser for SQLite and capture the Tickets table.*

![Figure 10: Database Tickets Table](screenshots/10-database.png)

---

### 4. REST API Endpoint Specifications

Base route: `/api/tickets`

| HTTP Method | API Endpoint | Description |
| :--- | :--- | :--- |
| **GET** | `/api/tickets` | Retrieve tickets with search, filter, sort and paging |
| **GET** | `/api/tickets/{id}` | Retrieve one ticket together with its comment thread |
| **GET** | `/api/tickets/stats` | Retrieve the summary counts used by the dashboard |
| **POST** | `/api/tickets` | Create a new support ticket (returns `201 Created`) |
| **PUT** | `/api/tickets/{id}` | Update an existing ticket |
| **PATCH** | `/api/tickets/{id}/status` | Change only the status of a ticket |
| **DELETE** | `/api/tickets/{id}` | Delete a ticket and its comments (returns `204 No Content`) |
| **GET** | `/api/tickets/{id}/comments` | Retrieve all comments for a ticket |
| **POST** | `/api/tickets/{id}/comments` | Add a comment to a ticket |
| **DELETE** | `/api/tickets/{id}/comments/{commentId}` | Delete a user comment |
| **GET** | `/health` | Check that the API and database are working |

**Query string parameters accepted by `GET /api/tickets`**

| Parameter | Example | Purpose |
| :--- | :--- | :--- |
| `search` | `vpn` | Matches title, description, requester or assignee |
| `status` | `Open` | Filter by status |
| `priority` | `Critical` | Filter by priority |
| `category` | `Network` | Filter by category |
| `assignedTo` | `unassigned` | Filter by assignee, or list unassigned tickets |
| `overdueOnly` | `true` | Show only tickets past their SLA date |
| `sortBy` / `sortDir` | `Priority` / `Asc` | Sorting |
| `page` / `pageSize` | `1` / `10` | Paging |

**Example request and response**

```bash
curl "http://localhost:5285/api/tickets?status=Open&priority=High"
```

```json
{
  "items": [
    {
      "id": 4,
      "title": "Access request: production analytics dashboard",
      "priority": "High",
      "status": "Open",
      "category": "Access",
      "raisedBy": "Sneha Nair",
      "assignedTo": "Identity Team",
      "dueDate": "2026-08-03T04:56:17Z",
      "isOverdue": true,
      "commentCount": 0
    }
  ],
  "page": 1,
  "pageSize": 10,
  "totalCount": 1,
  "totalPages": 1
}
```

---

### 5. Business Rules Implemented

| Rule | Description |
| :--- | :--- |
| Status on creation | Every new ticket is created as **Open**. The create DTO has no status field, so a client cannot create a ticket that is already closed. |
| SLA due date | Calculated from the priority — Critical = 4 hours, High = 24 hours, Medium = 72 hours, Low = 7 days. |
| Priority change | If the priority is changed, the due date is recalculated from the original creation time. |
| Resolution date | Set when a ticket becomes Resolved or Closed, and cleared again if the ticket is reopened. |
| History entries | Every status, priority or assignment change automatically adds a system comment recording who made the change. |
| System comments | Cannot be deleted by users, because they are the audit trail of the ticket. |
| Deleting a ticket | Also deletes all of its comments (cascade delete). |
| Time zone handling | All times are stored in UTC and converted to local time only when displayed. |

---

### 6. Unit Test Execution Results

The test project contains **37 tests** covering the service and repository layers, including SLA
calculation, status transitions, the history trail, cascade delete, search behaviour, sorting order
and paging.

Command used to run the tests:

```bash
dotnet test
```

**Test output:**

```
[ Paste the output of "dotnet test" here ]




```

**Screenshot of the test run:**

![Figure 11: Unit Test Results](screenshots/11-test-results.png)

**Summary table**

| Test Area | Number of Tests | Result |
| :--- | :--- | :--- |
| SLA due date calculation | 5 | _______ |
| Status transitions and resolution date | 4 | _______ |
| History / audit trail | 3 | _______ |
| Create and update rules | 5 | _______ |
| Comments | 4 | _______ |
| Statistics | 4 | _______ |
| Search, filter, sort and paging | 12 | _______ |
| **Total** | **37** | _______ |

---

### 7. How to Run the Project

**Requirement:** .NET 8 SDK. No database installation is needed — the SQLite file is created and
filled with sample data automatically the first time the API runs.

**Step 1 — start the Web API**

```bash
dotnet run --project src/HelpDesk.Api --urls http://localhost:5285
```

**Step 2 — start the MVC website (in a second terminal)**

```bash
dotnet run --project src/HelpDesk.Mvc --urls http://localhost:5185
```

**Step 3 — open in the browser**

| Application | Address |
| :--- | :--- |
| Help Desk website | `http://localhost:5185` |
| API documentation | `http://localhost:5285/swagger` |
| API health check | `http://localhost:5285/health` |

The Web API must be started first, because the website reads all of its data from the API.

---

### 8. Source Code Listings

The complete source code is available in the repository. The main files are listed below.

```
HelpDesk.sln
├── src/
│   ├── HelpDesk.Contracts/
│   │   ├── Enums.cs                 (TicketStatus, TicketPriority, TicketCategory)
│   │   ├── TicketDtos.cs            (Create / Update / Response DTOs)
│   │   └── QueryDtos.cs             (TicketQuery, PagedResult, TicketStatsDto)
│   ├── HelpDesk.Api/
│   │   ├── Models/Ticket.cs
│   │   ├── Models/TicketComment.cs
│   │   ├── Models/SlaPolicy.cs
│   │   ├── Data/HelpDeskDbContext.cs
│   │   ├── Data/DbSeeder.cs
│   │   ├── Repositories/ITicketRepository.cs
│   │   ├── Repositories/TicketRepository.cs
│   │   ├── Services/ITicketService.cs
│   │   ├── Services/TicketService.cs
│   │   ├── Controllers/TicketsController.cs
│   │   └── Program.cs
│   └── HelpDesk.Mvc/
│       ├── Controllers/TicketController.cs
│       ├── Services/TicketApiClient.cs
│       ├── Models/Display.cs
│       ├── Views/Ticket/*.cshtml
│       └── wwwroot/css/site.css
└── tests/
    └── HelpDesk.Tests/
        ├── TicketServiceTests.cs
        └── TicketRepositoryTests.cs
```

#### `src/HelpDesk.Contracts/Enums.cs`

```csharp
namespace HelpDesk.Contracts;

public enum TicketStatus
{
    Open,
    InProgress,
    Resolved,
    Closed
}

public enum TicketPriority
{
    Low,
    Medium,
    High,
    Critical
}

public enum TicketCategory
{
    Hardware,
    Software,
    Network,
    Access,
    Billing,
    Other
}
```

#### `src/HelpDesk.Api/Models/SlaPolicy.cs`

```csharp
namespace HelpDesk.Api.Models;

public static class SlaPolicy
{
    public static TimeSpan ResolutionWindowFor(TicketPriority priority) => priority switch
    {
        TicketPriority.Critical => TimeSpan.FromHours(4),
        TicketPriority.High     => TimeSpan.FromHours(24),
        TicketPriority.Medium   => TimeSpan.FromHours(72),
        TicketPriority.Low      => TimeSpan.FromDays(7),
        _                       => TimeSpan.FromDays(7)
    };

    public static DateTime DueDateFor(TicketPriority priority, DateTime createdAtUtc) =>
        createdAtUtc.Add(ResolutionWindowFor(priority));
}
```

#### `src/HelpDesk.Api/Repositories/ITicketRepository.cs`

```csharp
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
```

---

### 9. Conclusion

The Help Desk Management System meets all of the requirements set for the assignment. A REST Web API
was built using ASP.NET Core and Entity Framework Core with the Repository Pattern, and a separate
MVC web application consumes that API to provide the user interface.

Beyond the basic CRUD requirement, the project also implements searching, filtering, sorting, paging,
SLA tracking, a comment and history system, and a dashboard with summary reporting. Validation is
enforced on both the client and the server using a shared contracts project, so the rules cannot get
out of step between the two applications. Unit tests were written for the business rules to confirm
that the SLA calculations and status transitions behave correctly.

**Possible future improvements**

- User login and roles, so that employees and agents see different screens
- Email notifications when a ticket is assigned or resolved
- File attachments on tickets
- Moving from `EnsureCreated()` to Entity Framework migrations for schema changes

---

**Submitted by:** Rajrup Roy Chowdhury (IN26010404)
