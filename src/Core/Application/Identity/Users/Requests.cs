using System.ComponentModel.DataAnnotations;

namespace Care.WebApi.Application.Identity.Users;

public record CreateInviteRequest([Required, EmailAddress] string Email, string? PhoneNumber);

public record RegisterRequest(
    [Required] string Token,
    [Required] string Name,
    [Required, MinLength(8)] string Password,
    string? PhoneNumber);

public record ChangeUserRoleRequest([Required] string Role);

public record UpdatePhoneNumberRequest(string? PhoneNumber);

public record ForgotPasswordRequest([Required, EmailAddress] string Email);

public record ResetPasswordRequest(
    [Required, EmailAddress] string Email,
    [Required] string Token,
    [Required, MinLength(8)] string NewPassword);
