using System.Globalization;
using System.Security.Claims;
using System.Text.Encodings.Web;
using MaintenanceBoard.Api.Errors;
using MaintenanceBoard.Application.Access;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace MaintenanceBoard.Api.Authentication;

/// <summary>
/// Simulated authentication; replace with JWT/OIDC in production.
/// Only the authentication handler needs to change.
/// </summary>
public sealed class HeaderAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IUserRepository users) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "SimulatedUser";
    public const string HeaderName = "X-User-Id";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(HeaderName, out var values))
        {
            return AuthenticateResult.NoResult();
        }

        if (values.Count != 1 || !int.TryParse(values[0], NumberStyles.None, CultureInfo.InvariantCulture, out var userId) || userId <= 0)
        {
            return AuthenticateResult.Fail("The X-User-Id header must contain a single positive integer.");
        }

        var user = await users.GetByIdAsync(userId, Context.RequestAborted);
        if (user is null || !user.IsActive || !Enum.IsDefined(user.Role))
        {
            return AuthenticateResult.Fail("The X-User-Id header does not identify an active user.");
        }

        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString(CultureInfo.InvariantCulture)),
            new Claim(ClaimTypes.Name, user.UserName),
            new Claim(ClaimTypes.Role, user.Role.ToString())
        ], SchemeName);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.Headers.WWWAuthenticate = SchemeName;
        return ApiProblem.WriteAsync(Context, StatusCodes.Status401Unauthorized,
            "The X-User-Id header must identify an active user.", "authentication.required");
    }

    protected override Task HandleForbiddenAsync(AuthenticationProperties properties) =>
        ApiProblem.WriteAsync(Context, StatusCodes.Status403Forbidden,
            "The authenticated user is not allowed to perform this operation.", "access.forbidden");
}
