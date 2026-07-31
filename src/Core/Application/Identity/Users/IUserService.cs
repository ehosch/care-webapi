namespace Care.WebApi.Application.Identity.Users;

public interface IUserService
{
    Task<List<UserDto>> GetUsersAsync(CancellationToken cancellationToken);

    Task CreateInviteAsync(string email, string invitedByUserId, string origin, CancellationToken cancellationToken);

    Task ResendInviteAsync(string userId, string requestingUserId, string origin, CancellationToken cancellationToken);

    Task RevokeInviteAsync(string userId, CancellationToken cancellationToken);

    Task ChangeRoleAsync(string userId, string role, string requestingUserId, CancellationToken cancellationToken);

    Task RegisterAsync(string token, string name, string password, string? phoneNumber, CancellationToken cancellationToken);

    Task ForgotPasswordAsync(string email, string origin, CancellationToken cancellationToken);

    Task ResetPasswordAsync(string email, string token, string newPassword, CancellationToken cancellationToken);

    Task UpdatePhoneNumberAsync(string userId, string? phoneNumber, CancellationToken cancellationToken);
}
