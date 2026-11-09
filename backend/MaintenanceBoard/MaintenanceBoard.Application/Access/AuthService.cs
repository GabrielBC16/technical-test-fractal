using MaintenanceBoard.Application.Common;
using MaintenanceBoard.Application.Common.Exceptions;

namespace MaintenanceBoard.Application.Access;

public sealed class AuthService(IUserRepository users, IPasswordHasher passwordHasher) : IAuthService
{
    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var userName = InputValidation.RequiredText(request.UserName, "userName", 100);
        // Passwords are opaque: never trim or otherwise normalize them.
        var password = InputValidation.RequiredText(request.Password, "password", 1024, trim: false);
        var credentials = await users.GetCredentialsByUserNameAsync(userName, cancellationToken);

        if (credentials is null || !credentials.User.IsActive || !Enum.IsDefined(credentials.User.Role) ||
            string.IsNullOrWhiteSpace(credentials.PasswordHash) ||
            !passwordHasher.Verify(password, credentials.PasswordHash))
        {
            throw new UnauthenticatedUserException("Invalid user name or password.");
        }

        var user = credentials.User;
        return new LoginResponse(user.UserId, user.UserName, user.Names, user.LastNames, user.Role);
    }
}
