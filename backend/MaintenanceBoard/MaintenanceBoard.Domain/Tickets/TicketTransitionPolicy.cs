using System.Collections.Frozen;

namespace MaintenanceBoard.Domain.Tickets;

/// <summary>
/// Validates the maintenance ticket workflow without performing any I/O.
/// Assignment permissions, requester ownership, and assignment limits belong to application use cases.
/// </summary>
public sealed class TicketTransitionPolicy
{
    private static readonly FrozenSet<(TicketStatus From, TicketStatus To)> AllowedTransitions =
        new (TicketStatus, TicketStatus)[]
        {
            (TicketStatus.Pending, TicketStatus.InProgress),
            (TicketStatus.InProgress, TicketStatus.Closed),
            (TicketStatus.Pending, TicketStatus.Rejected),
            (TicketStatus.InProgress, TicketStatus.Rejected)
        }.ToFrozenSet();

    public bool CanTransition(TicketStatus currentStatus, TicketStatus newStatus) =>
        AllowedTransitions.Contains((currentStatus, newStatus));

    public void Validate(
        TicketStatus currentStatus,
        TicketStatus newStatus,
        int assigneeCount,
        string? cause = null,
        string? comment = null)
    {
        // Reject invalid pairs before their target-state guards, matching the persistence contract.
        if (!CanTransition(currentStatus, newStatus))
        {
            throw new TicketTransitionException(
                TicketTransitionViolation.InvalidTransition,
                $"The ticket cannot transition from '{currentStatus}' to '{newStatus}'.");
        }

        if (newStatus == TicketStatus.InProgress && assigneeCount < 1)
        {
            throw new TicketTransitionException(
                TicketTransitionViolation.AssigneeRequired,
                "Assign at least one operator before starting ticket attention.");
        }

        if (newStatus == TicketStatus.Closed && string.IsNullOrWhiteSpace(cause))
        {
            throw new TicketTransitionException(
                TicketTransitionViolation.CauseRequired,
                "A non-empty cause is required to close the ticket.");
        }

        if (newStatus == TicketStatus.Rejected && string.IsNullOrWhiteSpace(comment))
        {
            throw new TicketTransitionException(
                TicketTransitionViolation.RejectionReasonRequired,
                "A non-empty rejection reason is required.");
        }
    }

    public void Validate(
        string? currentStatus,
        string? newStatus,
        int assigneeCount,
        string? cause = null,
        string? comment = null)
    {
        // Enum.TryParse alone accepts numeric values and surrounding whitespace.
        // Exact names keep API/domain input consistent with the case-sensitive SQL status columns.
        if (!TryParseExactStatus(currentStatus, out var current) ||
            !TryParseExactStatus(newStatus, out var next))
        {
            throw new TicketTransitionException(
                TicketTransitionViolation.InvalidTransition,
                "Ticket statuses must be exactly Pending, InProgress, Closed, or Rejected.");
        }

        Validate(current, next, assigneeCount, cause, comment);
    }

    private static bool TryParseExactStatus(string? value, out TicketStatus status) =>
        Enum.TryParse(value, ignoreCase: false, out status) &&
        Enum.IsDefined(status) &&
        string.Equals(status.ToString(), value, StringComparison.Ordinal);
}
