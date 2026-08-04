namespace HelpDesk.Contracts;

/// <summary>Lifecycle state of a support ticket.</summary>
public enum TicketStatus
{
    Open,
    InProgress,
    Resolved,
    Closed
}

/// <summary>Business impact of a ticket. Drives the SLA due date.</summary>
public enum TicketPriority
{
    Low,
    Medium,
    High,
    Critical
}

/// <summary>Functional area the ticket belongs to.</summary>
public enum TicketCategory
{
    Hardware,
    Software,
    Network,
    Access,
    Billing,
    Other
}
