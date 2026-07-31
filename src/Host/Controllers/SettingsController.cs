using Care.WebApi.Application.Common.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Care.WebApi.Host.Controllers;

[ApiController]
[Route("api/settings")]
public class SettingsController : ControllerBase
{
    private readonly IAppSettingsService _appSettingsService;

    public SettingsController(IAppSettingsService appSettingsService)
    {
        _appSettingsService = appSettingsService;
    }

    [HttpGet]
    public async Task<ActionResult<AppSettingsDto>> GetSettingsAsync(CancellationToken cancellationToken) =>
        await _appSettingsService.GetSettingsAsync(cancellationToken);

    [HttpPut]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateSettingsAsync(UpdateSettingsRequest request, CancellationToken cancellationToken)
    {
        await _appSettingsService.UpdateSettingsAsync(request.PatientName, cancellationToken);
        return Ok();
    }
}
