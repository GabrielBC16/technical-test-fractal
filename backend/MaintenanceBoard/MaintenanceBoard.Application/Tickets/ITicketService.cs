namespace MaintenanceBoard.Application.Tickets;

public interface ITicketService
{
    Task<int> CreateAsync(CreateTicketRequest request, CancellationToken cancellationToken = default);
    Task<TicketBoard> GetBoardAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TicketListItem>> GetListAsync(TicketListRequest request, CancellationToken cancellationToken = default);
    Task<TicketDetails> GetDetailsAsync(int ticketId, CancellationToken cancellationToken = default);
    Task TransitionAsync(int ticketId, TransitionTicketRequest request, CancellationToken cancellationToken = default);
    Task AssignAsync(int ticketId, AssignTicketRequest request, CancellationToken cancellationToken = default);
    Task AddCommentAsync(int ticketId, AddTicketCommentRequest request, CancellationToken cancellationToken = default);
}
