using System.Security.Cryptography;
using Care.WebApi.Application.Common.Exceptions;
using Care.WebApi.Application.Common.Mailing;
using Care.WebApi.Application.Identity.Users;
using Care.WebApi.Domain.Identity;
using Care.WebApi.Infrastructure.Mailing;
using Care.WebApi.Infrastructure.Persistence.Context;
using Hangfire;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Care.WebApi.Infrastructure.Identity;

internal class UserService : IUserService
{
    private const int InviteExpirationDays = 7;

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _db;
    private readonly IBackgroundJobClient _jobClient;
    private readonly ILogger<UserService> _logger;

    public UserService(
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext db,
        IBackgroundJobClient jobClient,
        ILogger<UserService> logger)
    {
        _userManager = userManager;
        _db = db;
        _jobClient = jobClient;
        _logger = logger;
    }

    public async Task<List<UserDto>> GetUsersAsync(CancellationToken cancellationToken)
    {
        var users = await _userManager.Users.ToListAsync(cancellationToken);

        var result = new List<UserDto>();
        foreach (var user in users)
        {
            string role = (await _userManager.GetRolesAsync(user)).FirstOrDefault() ?? "Member";
            result.Add(new UserDto(user.Id, user.Name, user.Email!, role, user.Status.ToString(), user.InvitedAt, user.JoinedAt, user.PhoneNumber));
        }

        return result.OrderBy(u => u.Name).ToList();
    }

    public async Task CreateInviteAsync(string email, string invitedByUserId, string origin, CancellationToken cancellationToken)
    {
        email = email.Trim().ToLowerInvariant();

        if (await _userManager.FindByEmailAsync(email) is not null)
        {
            throw new ConflictException("A user with this email already exists.");
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            Name = email,
            Status = UserStatus.Invited,
            InvitedAt = DateTime.UtcNow,
            EmailConfirmed = true
        };

        var createResult = await _userManager.CreateAsync(user);
        if (!createResult.Succeeded)
        {
            throw new ConflictException(string.Join(" ", createResult.Errors.Select(e => e.Description)));
        }

        await _userManager.AddToRoleAsync(user, "Member");
        await CreateAndSendInviteAsync(user, invitedByUserId, origin, cancellationToken);
    }

    public async Task ResendInviteAsync(string userId, string requestingUserId, string origin, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(userId) ?? throw new NotFoundException("User not found.");
        if (user.Status != UserStatus.Invited)
        {
            throw new ConflictException("This user has already joined.");
        }

        await CreateAndSendInviteAsync(user, requestingUserId, origin, cancellationToken);
    }

    public async Task RevokeInviteAsync(string userId, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(userId) ?? throw new NotFoundException("User not found.");
        if (user.Status != UserStatus.Invited)
        {
            throw new ConflictException("This user has already joined; cannot revoke.");
        }

        var invites = _db.Invites.Where(i => i.Email == user.Email && i.UsedAt == null);
        _db.Invites.RemoveRange(invites);
        await _db.SaveChangesAsync(cancellationToken);

        await _userManager.DeleteAsync(user);
    }

    public async Task ChangeRoleAsync(string userId, string role, string requestingUserId, CancellationToken cancellationToken)
    {
        if (role != "Admin" && role != "Member")
        {
            throw new ConflictException("Role must be Admin or Member.");
        }

        if (userId == requestingUserId)
        {
            throw new ConflictException("You cannot change your own role.");
        }

        var user = await _userManager.FindByIdAsync(userId) ?? throw new NotFoundException("User not found.");

        var currentRoles = await _userManager.GetRolesAsync(user);
        await _userManager.RemoveFromRolesAsync(user, currentRoles);
        await _userManager.AddToRoleAsync(user, role);
    }

    public async Task RegisterAsync(string token, string name, string password, string? phoneNumber, CancellationToken cancellationToken)
    {
        var invite = await _db.Invites.FirstOrDefaultAsync(i => i.Token == token, cancellationToken)
            ?? throw new NotFoundException("Invite not found.");

        if (invite.UsedAt is not null || invite.ExpiresAt < DateTime.UtcNow)
        {
            throw new ConflictException("This invite has expired or already been used.");
        }

        var user = await _userManager.FindByEmailAsync(invite.Email)
            ?? throw new NotFoundException("Invited user not found.");

        var addPasswordResult = await _userManager.AddPasswordAsync(user, password);
        if (!addPasswordResult.Succeeded)
        {
            throw new ConflictException(string.Join(" ", addPasswordResult.Errors.Select(e => e.Description)));
        }

        user.Name = name;
        user.PhoneNumber = phoneNumber;
        user.Status = UserStatus.Active;
        user.JoinedAt = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);

        invite.UsedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdatePhoneNumberAsync(string userId, string? phoneNumber, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(userId) ?? throw new NotFoundException("User not found.");
        user.PhoneNumber = phoneNumber;
        await _userManager.UpdateAsync(user);
    }

    public async Task ForgotPasswordAsync(string email, string origin, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByEmailAsync(email.Trim().ToLowerInvariant());
        if (user is null || user.Status != UserStatus.Active)
        {
            // Don't reveal whether the account exists.
            return;
        }

        string token = await _userManager.GeneratePasswordResetTokenAsync(user);
        string link = $"{origin.TrimEnd('/')}/reset-password?token={Uri.EscapeDataString(token)}&email={Uri.EscapeDataString(user.Email!)}";

        _logger.LogInformation("Password reset link for {Email}: {Link}", user.Email, link);

        string body = EmailTemplates.PasswordResetEmail(link);
        _jobClient.Enqueue<IMailService>(m => m.SendAsync(
            new MailRequest(new List<string> { user.Email! }, "Reset your Care Coordination password", body),
            CancellationToken.None));
    }

    public async Task ResetPasswordAsync(string email, string token, string newPassword, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByEmailAsync(email.Trim().ToLowerInvariant())
            ?? throw new NotFoundException("Invalid request.");

        var result = await _userManager.ResetPasswordAsync(user, token, newPassword);
        if (!result.Succeeded)
        {
            throw new ConflictException(string.Join(" ", result.Errors.Select(e => e.Description)));
        }
    }

    private async Task CreateAndSendInviteAsync(ApplicationUser user, string requestingUserId, string origin, CancellationToken cancellationToken)
    {
        string token = GenerateSecureToken();
        var invite = new Invite
        {
            Email = user.Email!,
            Token = token,
            ExpiresAt = DateTime.UtcNow.AddDays(InviteExpirationDays),
            CreatedByUserId = requestingUserId
        };
        _db.Invites.Add(invite);
        await _db.SaveChangesAsync(cancellationToken);

        string link = $"{origin.TrimEnd('/')}/register?token={Uri.EscapeDataString(token)}";
        _logger.LogInformation("Invite link for {Email}: {Link}", user.Email, link);

        string body = EmailTemplates.InviteEmail(link);
        _jobClient.Enqueue<IMailService>(m => m.SendAsync(
            new MailRequest(new List<string> { user.Email! }, "You've been invited to Care Coordination", body),
            CancellationToken.None));
    }

    private static string GenerateSecureToken()
    {
        byte[] bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }
}
