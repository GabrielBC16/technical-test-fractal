using MaintenanceBoard.Application.Tickets;
using MaintenanceBoard.Domain.Tickets;

namespace MaintenanceBoard.Tests.Fakes;

internal sealed class FakeTicketRepository : ITicketRepository
{
    public Ticket? Ticket { get; set; } = ApplicationTestData.Ticket();
    public List<TicketAssignment> Assignments { get; } = [];
    public TicketDetails? Details { get; set; }
    public IReadOnlyList<TicketListItem> BoardItems { get; set; } = [];
    public IReadOnlyList<TicketListItem> ListItems { get; set; } = [];
    public TicketQueryFilter? LastFilter { get; private set; }
    public int WriteCalls { get; private set; }
    public int ReadCalls { get; private set; }
    public Exception? WriteException { get; set; }
    public CancellationToken LastCancellationToken { get; private set; }
    public CreateCall? Created { get; private set; }
    public TransitionCall? Transitioned { get; private set; }
    public AssignmentCall? Assigned { get; private set; }
    public CommentCall? Commented { get; private set; }

    public Task<Ticket?> GetByIdAsync(int ticketId, CancellationToken cancellationToken = default)
    {
        Read(cancellationToken);
        return Task.FromResult(Ticket?.TicketId == ticketId ? Ticket : null);
    }

    public Task<IReadOnlyList<TicketAssignment>> GetAssignmentsAsync(int ticketId, CancellationToken cancellationToken = default)
    {
        Read(cancellationToken);
        return Task.FromResult<IReadOnlyList<TicketAssignment>>(Assignments.Where(item => item.TicketId == ticketId).ToArray());
    }

    public Task<TicketDetails?> GetDetailsAsync(int ticketId, CancellationToken cancellationToken = default)
    {
        Read(cancellationToken);
        return Task.FromResult(Details?.Ticket.TicketId == ticketId ? Details : null);
    }

    public Task<IReadOnlyList<TicketListItem>> GetBoardAsync(CancellationToken cancellationToken = default)
    {
        Read(cancellationToken);
        return Task.FromResult(BoardItems);
    }

    public Task<IReadOnlyList<TicketListItem>> GetListAsync(TicketQueryFilter filter, CancellationToken cancellationToken = default)
    {
        Read(cancellationToken);
        LastFilter = filter;
        return Task.FromResult(ListItems);
    }

    public Task<int> CreateAsync(string title, string description, int reportedByUserId, CancellationToken cancellationToken = default)
    {
        Write(cancellationToken);
        Created = new CreateCall(title, description, reportedByUserId);
        return Task.FromResult(123);
    }

    public Task TransitionAsync(int ticketId, TicketStatus expectedStatus, TicketStatus newStatus, int actorUserId,
        string? comment, string? cause, CancellationToken cancellationToken = default)
    {
        Write(cancellationToken);
        Transitioned = new TransitionCall(ticketId, expectedStatus, newStatus, actorUserId, comment, cause);
        return Task.CompletedTask;
    }

    public Task AssignAsync(int ticketId, int assignedUserId, int actorUserId, string? justification, CancellationToken cancellationToken = default)
    {
        Write(cancellationToken);
        Assigned = new AssignmentCall(ticketId, assignedUserId, actorUserId, justification);
        return Task.CompletedTask;
    }

    public Task AddCommentAsync(int ticketId, int actorUserId, string comment, CancellationToken cancellationToken = default)
    {
        Write(cancellationToken);
        Commented = new CommentCall(ticketId, actorUserId, comment);
        return Task.CompletedTask;
    }

    private void Read(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        LastCancellationToken = cancellationToken;
        ReadCalls++;
    }

    private void Write(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        LastCancellationToken = cancellationToken;
        WriteCalls++;
        if (WriteException is not null)
        {
            throw WriteException;
        }
    }

    internal sealed record CreateCall(string Title, string Description, int ReportedByUserId);
    internal sealed record TransitionCall(int TicketId, TicketStatus ExpectedStatus, TicketStatus NewStatus, int ActorUserId, string? Comment, string? Cause);
    internal sealed record AssignmentCall(int TicketId, int AssignedUserId, int ActorUserId, string? Justification);
    internal sealed record CommentCall(int TicketId, int ActorUserId, string Comment);
}
