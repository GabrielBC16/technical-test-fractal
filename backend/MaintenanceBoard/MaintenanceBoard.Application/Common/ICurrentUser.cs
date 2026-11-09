namespace MaintenanceBoard.Application.Common;

// The API implementation reads NameIdentifier from the authenticated principal.
// Request payloads must never populate the actor identity.
public interface ICurrentUser
{
    int? UserId { get; }
}
