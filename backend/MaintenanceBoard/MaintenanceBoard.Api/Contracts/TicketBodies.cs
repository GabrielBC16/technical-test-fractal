using System.ComponentModel.DataAnnotations;

namespace MaintenanceBoard.Api.Contracts;

public sealed class CreateTicketBody
{
    [Required(ErrorMessage = "Title is required.")]
    [StringLength(200, ErrorMessage = "Title must not exceed 200 characters.")]
    public string? Title { get; init; }

    [Required(ErrorMessage = "Description is required.")]
    [StringLength(4000, ErrorMessage = "Description must not exceed 4000 characters.")]
    public string? Description { get; init; }
}

public sealed class TransitionTicketBody
{
    [Required(ErrorMessage = "Expected status is required.")]
    [StringLength(20, ErrorMessage = "Expected status must not exceed 20 characters.")]
    public string? ExpectedStatus { get; init; }

    [Required(ErrorMessage = "New status is required.")]
    [StringLength(20, ErrorMessage = "New status must not exceed 20 characters.")]
    public string? NewStatus { get; init; }

    [StringLength(4000, ErrorMessage = "Comment must not exceed 4000 characters.")]
    public string? Comment { get; init; }

    [StringLength(4000, ErrorMessage = "Cause must not exceed 4000 characters.")]
    public string? Cause { get; init; }
}

public sealed class AssignTicketBody
{
    [Range(1, int.MaxValue, ErrorMessage = "Assigned user ID must be a positive integer.")]
    public int AssignedUserId { get; init; }

    [StringLength(2000, ErrorMessage = "Justification must not exceed 2000 characters.")]
    public string? Justification { get; init; }
}

public sealed class AddTicketCommentBody
{
    [Required(ErrorMessage = "Comment is required.")]
    [StringLength(4000, ErrorMessage = "Comment must not exceed 4000 characters.")]
    public string? Comment { get; init; }
}

public sealed class TicketListQuery
{
    [StringLength(20, ErrorMessage = "Status must not exceed 20 characters.")]
    public string? Status { get; init; }
    public DateTimeOffset? From { get; init; }
    public DateTimeOffset? To { get; init; }
}

public sealed record CreatedTicketResponse(int TicketId);
