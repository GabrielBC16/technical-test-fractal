using System.ComponentModel.DataAnnotations;
using MaintenanceBoard.Api.Contracts;
using MaintenanceBoard.Application.Tickets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MaintenanceBoard.Api.Controllers;

/// <summary>Creates and tracks maintenance tickets through their allowed workflow.</summary>
[ApiController]
[Authorize]
[Route("api/tickets")]
public sealed class TicketsController(ITicketService tickets) : ControllerBase
{
    /// <summary>Creates a Pending ticket and its initial history event.</summary>
    [HttpPost]
    [Authorize(Roles = "Requester,Operator,Supervisor")]
    [ProducesResponseType<CreatedTicketResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<CreatedTicketResponse>> Create(
        [FromBody] CreateTicketBody body, CancellationToken cancellationToken)
    {
        var ticketId = await tickets.CreateAsync(new CreateTicketRequest(body.Title, body.Description), cancellationToken);
        return Created($"/api/tickets/{ticketId}", new CreatedTicketResponse(ticketId));
    }

    /// <summary>Returns the main board with Pending, InProgress, and Closed tickets only.</summary>
    [HttpGet("board")]
    [ProducesResponseType<TicketBoard>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<TicketBoard>> GetBoard(CancellationToken cancellationToken) =>
        Ok(await tickets.GetBoardAsync(cancellationToken));

    /// <summary>Lists tickets with optional exact status and UTC creation-date boundaries.</summary>
    /// <remarks>From is inclusive and To is exclusive. Rejected tickets may appear here.</remarks>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<TicketListItem>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<TicketListItem>>> GetList(
        [FromQuery] TicketListQuery query, CancellationToken cancellationToken) =>
        Ok(await tickets.GetListAsync(new TicketListRequest(query.Status, query.From, query.To), cancellationToken));

    /// <summary>Returns a ticket, its assigned users, and its complete chronological history.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType<TicketDetails>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TicketDetails>> GetById(
        [Range(1, int.MaxValue, ErrorMessage = "Ticket ID must be a positive integer.")] int id,
        CancellationToken cancellationToken) =>
        Ok(await tickets.GetDetailsAsync(id, cancellationToken));

    /// <summary>Changes status using the expected current status for concurrency control.</summary>
    /// <remarks>Starting requires an assignee; closing requires Cause; rejection requires Comment.</remarks>
    [HttpPost("{id:int}/transitions")]
    [Authorize(Roles = "Operator,Supervisor")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Transition(
        [Range(1, int.MaxValue, ErrorMessage = "Ticket ID must be a positive integer.")] int id,
        [FromBody] TransitionTicketBody body, CancellationToken cancellationToken)
    {
        await tickets.TransitionAsync(id, new TransitionTicketRequest(
            body.ExpectedStatus, body.NewStatus, body.Comment, body.Cause), cancellationToken);
        return NoContent();
    }

    /// <summary>Assigns an active Operator or Supervisor to an open ticket.</summary>
    /// <remarks>At most three assignees are allowed. Additional assignees require Justification.</remarks>
    [HttpPost("{id:int}/assignments")]
    [Authorize(Roles = "Operator,Supervisor")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Assign(
        [Range(1, int.MaxValue, ErrorMessage = "Ticket ID must be a positive integer.")] int id,
        [FromBody] AssignTicketBody body, CancellationToken cancellationToken)
    {
        await tickets.AssignAsync(id, new AssignTicketRequest(body.AssignedUserId, body.Justification), cancellationToken);
        return NoContent();
    }

    /// <summary>Adds a comment without changing ticket status.</summary>
    /// <remarks>Requesters may comment only on tickets they reported.</remarks>
    [HttpPost("{id:int}/comments")]
    [Authorize(Roles = "Requester,Operator,Supervisor")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Comment(
        [Range(1, int.MaxValue, ErrorMessage = "Ticket ID must be a positive integer.")] int id,
        [FromBody] AddTicketCommentBody body, CancellationToken cancellationToken)
    {
        await tickets.AddCommentAsync(id, new AddTicketCommentRequest(body.Comment), cancellationToken);
        return NoContent();
    }
}
