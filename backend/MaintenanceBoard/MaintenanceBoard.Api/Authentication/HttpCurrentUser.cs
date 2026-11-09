using System.Globalization;
using System.Security.Claims;
using MaintenanceBoard.Application.Common;

namespace MaintenanceBoard.Api.Authentication;

public sealed class HttpCurrentUser(IHttpContextAccessor context) : ICurrentUser
{
    public int? UserId
    {
        get
        {
            var principal = context.HttpContext?.User;
            if (principal?.Identity?.IsAuthenticated != true)
            {
                return null;
            }

            return int.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier),
                NumberStyles.None, CultureInfo.InvariantCulture, out var userId) && userId > 0 ? userId : null;
        }
    }
}
