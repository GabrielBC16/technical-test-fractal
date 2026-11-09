using MaintenanceBoard.Domain.Tickets;

namespace MaintenanceBoard.Application.Tickets;

public interface ITicketRepository
{
    Task<Ticket?> GetByIdAsync(int ticketId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TicketAssignment>> GetAssignmentsAsync(int ticketId, CancellationToken cancellationToken = default);
    Task<TicketDetails?> GetDetailsAsync(int ticketId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TicketListItem>> GetBoardAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TicketListItem>> GetListAsync(TicketQueryFilter filter, CancellationToken cancellationToken = default);

    Task<int> CreateAsync(string title, string description, int reportedByUserId, CancellationToken cancellationToken = default);
    Task TransitionAsync(int ticketId, TicketStatus expectedStatus, TicketStatus newStatus, int actorUserId,
        string? comment, string? cause, CancellationToken cancellationToken = default);
    Task AssignAsync(int ticketId, int assignedUserId, int actorUserId, string? justification, CancellationToken cancellationToken = default);
    Task AddCommentAsync(int ticketId, int actorUserId, string comment, CancellationToken cancellationToken = default);
}
