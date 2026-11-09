using MaintenanceBoard.Application.Common;

namespace MaintenanceBoard.Tests.Fakes;

internal sealed class FakeCurrentUser(int? userId = 10) : ICurrentUser
{
    public int? UserId { get; set; } = userId;
}
