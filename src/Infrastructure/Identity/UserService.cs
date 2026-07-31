using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Care.WebApi.Application.Common.Exceptions;
using Care.WebApi.Application.Common.Mailing;
using Care.WebApi.Application.Common.Sms;
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
            result.Add(new UserDto(user.Id, user.Name, user.Email, role, user.Status.ToString(), user.InvitedAt, user.JoinedAt, user.PhoneNumber));
        }

        return result.OrderBy(u => u.Name).ToList();
    }

    public async Task CreateInviteAsync(string? email, string? phoneNumber, string invitedByUserId, string origin, CancellationToken cancellationToken)
    {
        email = string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLowerInvariant();
        phoneNumber = string.IsNullOrWhiteSpace(phoneNumber) ? null : phoneNumber;

        ApplicationUser user;
        if (email is not null)
        {
            if (await _userManager.FindByEmailAsync(email) is not null)
            {
                throw new ConflictException("A user with this email already exists.");
            }

            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                Name = email,
                PhoneNumber = phoneNumber,
                Status = UserStatus.Invited,
                InvitedAt = DateTime.UtcNow,
                EmailConfirmed = true
            };
        }
        else
        {
            if (await _userManager.Users.AnyAsync(u => u.PhoneNumber == phoneNumber, cancellationToken))
            {
                throw new ConflictException("A user with this phone number already exists.");
            }

            // No email yet — the invitee sets one on the Register page. UserName still has to be
            // unique and non-null for Identity, so a sanitized phone number stands in for it.
            string sanitizedPhone = Regex.Replace(phoneNumber!, @"[^\d+]", "");
            user = new ApplicationUser
            {
                UserName = sanitizedPhone,
                Email = null,
                Name = phoneNumber!,
                PhoneNumber = phoneNumber,
                Status = UserStatus.Invited,
                InvitedAt = DateTime.UtcNow,
                EmailConfirmed = false
            };
        }

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

        var invites = _db.Invites.Where(i => i.UserId == user.Id && i.UsedAt == null);
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

    public async Task<InviteInfoDto> GetInviteInfoAsync(string token, CancellationToken cancellationToken)
    {
        var invite = await _db.Invites.FirstOrDefaultAsync(i => i.Token == token, cancellationToken)
            ?? throw new NotFoundException("Invite not found.");

        if (invite.UsedAt is not null || invite.ExpiresAt < DateTime.UtcNow)
        {
            throw new ConflictException("This invite has expired or already been used.");
        }

        return new InviteInfoDto(RequiresEmail: invite.Email is null);
    }

    public async Task RegisterAsync(string token, string name, string password, string? phoneNumber, string? email, CancellationToken cancellationToken)
    {
        var invite = await _db.Invites.FirstOrDefaultAsync(i => i.Token == token, cancellationToken)
            ?? throw new NotFoundException("Invite not found.");

        if (invite.UsedAt is not null || invite.ExpiresAt < DateTime.UtcNow)
        {
            throw new ConflictException("This invite has expired or already been used.");
        }

        var user = await _userManager.FindByIdAsync(invite.UserId)
            ?? throw new NotFoundException("Invited user not found.");

        if (user.Email is null)
        {
            email = string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLowerInvariant();
            if (email is null)
            {
                throw new ConflictException("Email is required.");
            }

            if (await _userManager.FindByEmailAsync(email) is not null)
            {
                throw new ConflictException("A user with this email already exists.");
            }

            var setEmailResult = await _userManager.SetEmailAsync(user, email);
            if (!setEmailResult.Succeeded)
            {
                throw new ConflictException(string.Join(" ", setEmailResult.Errors.Select(e => e.Description)));
            }

            await _userManager.SetUserNameAsync(user, email);
            user.EmailConfirmed = true;
        }

        var addPasswordResult = await _userManager.AddPasswordAsync(user, password);
        if (!addPasswordResult.Succeeded)
        {
            throw new ConflictException(string.Join(" ", addPasswordResult.Errors.Select(e => e.Description)));
        }

        user.Name = name;
        if (!string.IsNullOrWhiteSpace(phoneNumber))
        {
            user.PhoneNumber = phoneNumber;
        }

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

    public async Task<UserDto> GetUserAsync(string userId, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(userId) ?? throw new NotFoundException("User not found.");
        string role = (await _userManager.GetRolesAsync(user)).FirstOrDefault() ?? "Member";
        return new UserDto(user.Id, user.Name, user.Email, role, user.Status.ToString(), user.InvitedAt, user.JoinedAt, user.PhoneNumber);
    }

    public async Task ChangePasswordAsync(string userId, string currentPassword, string newPassword, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(userId) ?? throw new NotFoundException("User not found.");
        var result = await _userManager.ChangePasswordAsync(user, currentPassword, newPassword);
        if (!result.Succeeded)
        {
            throw new ConflictException(string.Join(" ", result.Errors.Select(e => e.Description)));
        }
    }

    public async Task RequestEmailChangeAsync(string userId, string newEmail, string origin, CancellationToken cancellationToken)
    {
        newEmail = newEmail.Trim().ToLowerInvariant();
        var user = await _userManager.FindByIdAsync(userId) ?? throw new NotFoundException("User not found.");

        if (await _userManager.FindByEmailAsync(newEmail) is not null)
        {
            throw new ConflictException("A user with this email already exists.");
        }

        string token = await _userManager.GenerateChangeEmailTokenAsync(user, newEmail);
        string link = $"{origin.TrimEnd('/')}/confirm-email-change?userId={Uri.EscapeDataString(user.Id)}&newEmail={Uri.EscapeDataString(newEmail)}&token={Uri.EscapeDataString(token)}";

        _logger.LogInformation("Email change confirmation link for {UserId} -> {NewEmail}: {Link}", user.Id, newEmail, link);

        string body = EmailTemplates.EmailChangeConfirmationEmail(link);
        _jobClient.Enqueue<IMailService>(m => m.SendAsync(
            new MailRequest(new List<string> { newEmail }, "Confirm your new Care Coordination email", body),
            CancellationToken.None));
    }

    public async Task ConfirmEmailChangeAsync(string userId, string newEmail, string token, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(userId) ?? throw new NotFoundException("Invalid request.");
        newEmail = newEmail.Trim().ToLowerInvariant();

        var result = await _userManager.ChangeEmailAsync(user, newEmail, token);
        if (!result.Succeeded)
        {
            throw new ConflictException(string.Join(" ", result.Errors.Select(e => e.Description)));
        }

        await _userManager.SetUserNameAsync(user, newEmail);
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
            UserId = user.Id,
            Email = user.Email,
            Token = token,
            ExpiresAt = DateTime.UtcNow.AddDays(InviteExpirationDays),
            CreatedByUserId = requestingUserId
        };
        _db.Invites.Add(invite);
        await _db.SaveChangesAsync(cancellationToken);

        string link = $"{origin.TrimEnd('/')}/register?token={Uri.EscapeDataString(token)}";
        _logger.LogInformation("Invite link for {Email}: {Link}", (object?)user.Email ?? user.PhoneNumber, link);

        string? patientName = (await _db.AppSettings.FirstOrDefaultAsync(cancellationToken))?.PatientName;

        if (user.Email is not null)
        {
            string body = EmailTemplates.InviteEmail(link, patientName);
            _jobClient.Enqueue<IMailService>(m => m.SendAsync(
                new MailRequest(new List<string> { user.Email }, "You've been invited to Care Coordination", body),
                CancellationToken.None));
        }

        if (!string.IsNullOrEmpty(user.PhoneNumber))
        {
            string forWhom = string.IsNullOrWhiteSpace(patientName) ? "" : $" for {patientName}";
            string smsBody = $"You've been invited to help coordinate care{forWhom}. Set up your account: {link}";
            _jobClient.Enqueue<ISmsService>(s => s.SendAsync(
                new SmsRequest(user.PhoneNumber, smsBody),
                CancellationToken.None));
        }
    }

    private static string GenerateSecureToken()
    {
        byte[] bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }
}
