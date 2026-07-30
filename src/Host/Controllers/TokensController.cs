using Care.WebApi.Application.Identity.Tokens;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Care.WebApi.Host.Controllers;

[ApiController]
[Route("api/tokens")]
public class TokensController : ControllerBase
{
    private readonly ITokenService _tokenService;

    public TokensController(ITokenService tokenService)
    {
        _tokenService = tokenService;
    }

    [AllowAnonymous]
    [HttpPost]
    public async Task<ActionResult<TokenResponse>> GetTokenAsync(TokenRequest request, CancellationToken cancellationToken)
    {
        string ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return await _tokenService.GetTokenAsync(request, ipAddress, cancellationToken);
    }

    [AllowAnonymous]
    [HttpPost("refresh")]
    public async Task<ActionResult<TokenResponse>> RefreshAsync(RefreshTokenRequest request)
    {
        string ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return await _tokenService.RefreshTokenAsync(request, ipAddress);
    }
}
