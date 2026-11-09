using System.ComponentModel.DataAnnotations;

namespace MaintenanceBoard.Api.Contracts;

public sealed class LoginBody
{
    [Required(ErrorMessage = "User name is required.")]
    [StringLength(100, ErrorMessage = "User name must not exceed 100 characters.")]
    public string? UserName { get; init; }

    [Required(ErrorMessage = "Password is required.")]
    [StringLength(1024, ErrorMessage = "Password must not exceed 1024 characters.")]
    public string? Password { get; init; }
}
