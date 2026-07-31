using System.ComponentModel.DataAnnotations;

namespace Care.WebApi.Application.Identity.Users;

public record CreateInviteRequest(
    [EmailAddress(ErrorMessage = "Enter a valid email address.")] string? Email,
    string? PhoneNumber) : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(Email) && string.IsNullOrWhiteSpace(PhoneNumber))
        {
            yield return new ValidationResult("Provide an email or phone number.");
        }
    }
}

public record RegisterRequest(
    [Required] string Token,
    [Required(ErrorMessage = "Name is required.")] string Name,
    [Required(ErrorMessage = "Password is required."), MinLength(8, ErrorMessage = "Password must be at least 8 characters.")] string Password,
    string? PhoneNumber,
    [EmailAddress(ErrorMessage = "Enter a valid email address.")] string? Email);

public record ChangeUserRoleRequest([Required] string Role);

public record UpdatePhoneNumberRequest(string? PhoneNumber);

public record ForgotPasswordRequest([Required(ErrorMessage = "Email is required."), EmailAddress(ErrorMessage = "Enter a valid email address.")] string Email);

public record ResetPasswordRequest(
    [Required(ErrorMessage = "Email is required."), EmailAddress(ErrorMessage = "Enter a valid email address.")] string Email,
    [Required] string Token,
    [Required(ErrorMessage = "Password is required."), MinLength(8, ErrorMessage = "Password must be at least 8 characters.")] string NewPassword);

public record ChangePasswordRequest(
    [Required(ErrorMessage = "Current password is required.")] string CurrentPassword,
    [Required(ErrorMessage = "New password is required."), MinLength(8, ErrorMessage = "Password must be at least 8 characters.")] string NewPassword);

public record RequestEmailChangeRequest(
    [Required(ErrorMessage = "Email is required."), EmailAddress(ErrorMessage = "Enter a valid email address.")] string NewEmail);

public record ConfirmEmailChangeRequest(
    [Required] string UserId,
    [Required(ErrorMessage = "Email is required."), EmailAddress(ErrorMessage = "Enter a valid email address.")] string NewEmail,
    [Required] string Token);
