using System.Security.Claims;
using Care.WebApi.Application.Care;
using Microsoft.AspNetCore.Mvc;

namespace Care.WebApi.Host.Controllers;

[ApiController]
[Route("api/replacement-requests")]
public class ReplacementRequestsController : ControllerBase
{
    private readonly IShiftService _shiftService;

    public ReplacementRequestsController(IShiftService shiftService)
    {
        _shiftService = shiftService;
    }

    [HttpGet]
    public async Task<ActionResult<List<ReplacementRequestDto>>> GetQueueAsync(CancellationToken cancellationToken) =>
        await _shiftService.GetReplacementQueueAsync(cancellationToken);

    [HttpPost("{id}/claim")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ClaimAsync(Guid id, CancellationToken cancellationToken)
    {
        await _shiftService.ClaimReplacementRequestAsync(id, RequestingUserId, cancellationToken);
        return Ok();
    }

    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> CancelAsync(Guid id, CancellationToken cancellationToken)
    {
        await _shiftService.CancelReplacementRequestAsync(id, RequestingUserId, cancellationToken);
        return Ok();
    }

    private string RequestingUserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
}
