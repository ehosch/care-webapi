using System.Security.Claims;
using Care.WebApi.Application.Identity.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Care.WebApi.Host.Controllers;

[ApiController]
[Route("api/users")]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;

    public UsersController(IUserService userService)
    {
        _userService = userService;
    }

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<List<UserDto>>> GetUsersAsync(CancellationToken cancellationToken) =>
        await _userService.GetUsersAsync(cancellationToken);

    [HttpPost("invite")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> InviteAsync(CreateInviteRequest request, CancellationToken cancellationToken)
    {
        await _userService.CreateInviteAsync(request.Email, RequestingUserId, Origin, cancellationToken);
        return Ok();
    }

    [HttpPost("{id}/resend-invite")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ResendInviteAsync(string id, CancellationToken cancellationToken)
    {
        await _userService.ResendInviteAsync(id, RequestingUserId, Origin, cancellationToken);
        return Ok();
    }

    [HttpDelete("{id}/invite")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> RevokeInviteAsync(string id, CancellationToken cancellationToken)
    {
        await _userService.RevokeInviteAsync(id, cancellationToken);
        return Ok();
    }

    [HttpPut("{id}/role")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ChangeRoleAsync(string id, ChangeUserRoleRequest request, CancellationToken cancellationToken)
    {
        await _userService.ChangeRoleAsync(id, request.Role, RequestingUserId, cancellationToken);
        return Ok();
    }

    [AllowAnonymous]
    [HttpPost("register")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        await _userService.RegisterAsync(request.Token, request.Name, request.Password, cancellationToken);
        return Ok();
    }

    [AllowAnonymous]
    [HttpPost("forgot-password")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        await _userService.ForgotPasswordAsync(request.Email, Origin, cancellationToken);
        return Ok();
    }

    [AllowAnonymous]
    [HttpPost("reset-password")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        await _userService.ResetPasswordAsync(request.Email, request.Token, request.NewPassword, cancellationToken);
        return Ok();
    }

    private string RequestingUserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    private string Origin => Request.Headers.Origin.FirstOrDefault() ?? $"{Request.Scheme}://{Request.Host}";
}
