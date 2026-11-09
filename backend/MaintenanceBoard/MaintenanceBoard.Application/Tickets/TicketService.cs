using MaintenanceBoard.Application.Access;
using MaintenanceBoard.Application.Common;
using MaintenanceBoard.Application.Common.Exceptions;
using MaintenanceBoard.Application.Tickets.Exceptions;
using MaintenanceBoard.Domain.Access;
using MaintenanceBoard.Domain.Tickets;

namespace MaintenanceBoard.Application.Tickets;

public sealed class TicketService(
    ITicketRepository tickets,
    IUserRepository users,
    ICurrentUser currentUser,
    TicketTransitionPolicy transitionPolicy) : ITicketService
{
    private readonly AuthenticatedUserResolver _actors = new(users, currentUser);

    public async Task<int> CreateAsync(CreateTicketRequest request, CancellationToken cancellationToken = default)
    {
        var actor = await _actors.RequireAsync(cancellationToken);
        var title = InputValidation.RequiredText(request.Title, "title", 200);
        var description = InputValidation.RequiredText(request.Description, "description", 4000);

        return await tickets.CreateAsync(title, description, actor.UserId, cancellationToken);
    }

    public async Task<TicketBoard> GetBoardAsync(CancellationToken cancellationToken = default)
    {
        // All active roles may read tickets. Requester ownership restricts comments only.
        await _actors.RequireAsync(cancellationToken);
        var items = await tickets.GetBoardAsync(cancellationToken);

        // The repository also excludes Rejected in SQL. Grouping here keeps the board contract explicit.
        return new TicketBoard(
            Order(items.Where(item => item.Status == TicketStatus.Pending)),
            Order(items.Where(item => item.Status == TicketStatus.InProgress)),
            Order(items.Where(item => item.Status == TicketStatus.Closed)));
    }

    public async Task<IReadOnlyList<TicketListItem>> GetListAsync(
        TicketListRequest request, CancellationToken cancellationToken = default)
    {
        await _actors.RequireAsync(cancellationToken);
        TicketStatus? status = null;

        if (request.Status is not null)
        {
            if (!Enum.TryParse<TicketStatus>(request.Status, ignoreCase: false, out var parsed) ||
                !Enum.IsDefined(parsed) || !string.Equals(parsed.ToString(), request.Status, StringComparison.Ordinal))
            {
                throw new RequestValidationException("status", "Status must be Pending, InProgress, Closed, or Rejected.");
            }

            status = parsed;
        }

        if (request.From is not null && request.To is not null && request.From >= request.To)
        {
            throw new RequestValidationException("to", "The exclusive end date must be later than the start date.");
        }

        var filter = new TicketQueryFilter(status, request.From?.UtcDateTime, request.To?.UtcDateTime);
        return await tickets.GetListAsync(filter, cancellationToken);
    }

    public async Task<TicketDetails> GetDetailsAsync(int ticketId, CancellationToken cancellationToken = default)
    {
        await _actors.RequireAsync(cancellationToken);
        InputValidation.PositiveId(ticketId, "ticketId");
        var details = await tickets.GetDetailsAsync(ticketId, cancellationToken)
            ?? throw new TicketNotFoundException();

        return details with
        {
            History = details.History.OrderBy(item => item.CreatedAtUtc).ThenBy(item => item.HistoryId).ToArray()
        };
    }

    public async Task TransitionAsync(
        int ticketId, TransitionTicketRequest request, CancellationToken cancellationToken = default)
    {
        var actor = await _actors.RequireAsync(cancellationToken, UserRole.Operator, UserRole.Supervisor);
        var ticket = await RequireTicketAsync(ticketId, cancellationToken);
        var expectedStatus = InputValidation.RequiredText(request.ExpectedStatus, "expectedStatus", 20, trim: false);
        var newStatus = InputValidation.RequiredText(request.NewStatus, "newStatus", 20, trim: false);

        if (!string.Equals(ticket.Status.ToString(), expectedStatus, StringComparison.Ordinal))
        {
            throw new TicketConcurrencyException();
        }

        var cause = InputValidation.OptionalText(request.Cause, "cause", 4000);
        var comment = InputValidation.OptionalText(request.Comment, "comment", 4000);
        var assignments = await tickets.GetAssignmentsAsync(ticketId, cancellationToken);

        try
        {
            transitionPolicy.Validate(ticket.Status.ToString(), newStatus, assignments.Count, cause, comment);
        }
        catch (TicketTransitionException exception)
        {
            throw TranslateTransitionException(exception);
        }

        // The SP rechecks the expected status and all guards under the parent lock.
        // Application validation deliberately does not claim to replace that transaction.
        await tickets.TransitionAsync(ticketId, ticket.Status, Enum.Parse<TicketStatus>(newStatus),
            actor.UserId, comment, cause, cancellationToken);
    }

    public async Task AssignAsync(
        int ticketId, AssignTicketRequest request, CancellationToken cancellationToken = default)
    {
        var actor = await _actors.RequireAsync(cancellationToken, UserRole.Operator, UserRole.Supervisor);
        var ticket = await RequireTicketAsync(ticketId, cancellationToken);
        InputValidation.PositiveId(request.AssignedUserId, "assignedUserId");

        if (ticket.Status is not (TicketStatus.Pending or TicketStatus.InProgress))
        {
            throw new TicketAssignmentConflictException("Only pending or in-progress tickets can be assigned.");
        }

        var assignee = await users.GetByIdAsync(request.AssignedUserId, cancellationToken);
        if (assignee is null || !assignee.IsActive || assignee.Role is not (UserRole.Operator or UserRole.Supervisor))
        {
            throw new InvalidTicketAssigneeException();
        }

        var assignments = await tickets.GetAssignmentsAsync(ticketId, cancellationToken);
        if (assignments.Any(assignment => assignment.AssignedUserId == request.AssignedUserId))
        {
            throw new TicketAssignmentConflictException("The user is already assigned to this ticket.");
        }

        if (assignments.Count >= 3)
        {
            throw new TicketAssignmentLimitException();
        }

        if (assignments.Count >= 1 && string.IsNullOrWhiteSpace(request.Justification))
        {
            throw new TicketAssignmentJustificationRequiredException();
        }

        var justification = InputValidation.OptionalText(request.Justification, "justification", 2000);
        await tickets.AssignAsync(ticketId, assignee.UserId, actor.UserId, justification, cancellationToken);
    }

    public async Task AddCommentAsync(
        int ticketId, AddTicketCommentRequest request, CancellationToken cancellationToken = default)
    {
        var actor = await _actors.RequireAsync(cancellationToken);
        var ticket = await RequireTicketAsync(ticketId, cancellationToken);

        if (actor.Role == UserRole.Requester && ticket.ReportedByUserId != actor.UserId)
        {
            throw new ForbiddenOperationException("Requesters can comment only on tickets they reported.");
        }

        if (string.IsNullOrWhiteSpace(request.Comment))
        {
            throw new TicketCommentRequiredException();
        }

        InputValidation.CheckLength(request.Comment, "comment", 4000);
        await tickets.AddCommentAsync(ticketId, actor.UserId, request.Comment.Trim(), cancellationToken);
    }

    private async Task<Ticket> RequireTicketAsync(int ticketId, CancellationToken cancellationToken)
    {
        InputValidation.PositiveId(ticketId, "ticketId");
        return await tickets.GetByIdAsync(ticketId, cancellationToken) ?? throw new TicketNotFoundException();
    }

    private static TicketListItem[] Order(IEnumerable<TicketListItem> items) =>
        items.OrderByDescending(item => item.CreatedAtUtc).ThenByDescending(item => item.TicketId).ToArray();

    private static ApplicationFailureException TranslateTransitionException(TicketTransitionException exception) =>
        exception.Violation switch
        {
            TicketTransitionViolation.CauseRequired => new TicketCauseRequiredException(exception.Message, exception),
            TicketTransitionViolation.RejectionReasonRequired => new TicketRejectionReasonRequiredException(exception.Message, exception),
            _ => new InvalidTicketTransitionException(exception.Message, exception)
        };
}
