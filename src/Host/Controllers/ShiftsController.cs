using Care.WebApi.Application.Care;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Care.WebApi.Host.Controllers;

[ApiController]
[Route("api/shifts")]
public class ShiftsController : ControllerBase
{
    private readonly IShiftService _shiftService;

    public ShiftsController(IShiftService shiftService)
    {
        _shiftService = shiftService;
    }

    [HttpGet]
    public async Task<ActionResult<List<ShiftDto>>> GetShiftsAsync(DateOnly weekStart, CancellationToken cancellationToken) =>
        await _shiftService.GetShiftsAsync(weekStart, cancellationToken);

    [HttpPut("{id}/assign")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> AssignAsync(Guid id, AssignShiftRequest request, CancellationToken cancellationToken)
    {
        await _shiftService.AssignShiftAsync(id, request.UserId, cancellationToken);
        return Ok();
    }
}
