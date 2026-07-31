using System.Security.Claims;
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

    [HttpPost("{id}/claim")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ClaimAsync(Guid id, CancellationToken cancellationToken)
    {
        await _shiftService.ClaimShiftAsync(id, RequestingUserId, cancellationToken);
        return Ok();
    }

    [HttpPost("{id}/replacement-requests")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateReplacementRequestAsync(Guid id, CreateReplacementRequestRequest request, CancellationToken cancellationToken)
    {
        await _shiftService.CreateReplacementRequestAsync(id, RequestingUserId, request.Reason, cancellationToken);
        return Ok();
    }

    [HttpGet("{id}/notes")]
    public async Task<ActionResult<List<ShiftNoteDto>>> GetNotesAsync(Guid id, CancellationToken cancellationToken) =>
        await _shiftService.GetShiftNotesAsync(id, cancellationToken);

    [HttpPost("{id}/notes")]
    public async Task<ActionResult<ShiftNoteDto>> AddNoteAsync(Guid id, AddShiftNoteRequest request, CancellationToken cancellationToken) =>
        await _shiftService.AddShiftNoteAsync(id, RequestingUserId, request.Text, cancellationToken);

    [HttpPut("{id}/times")]
    public async Task<ActionResult<ShiftDto>> AdjustTimesAsync(Guid id, AdjustShiftTimesRequest request, CancellationToken cancellationToken) =>
        await _shiftService.AdjustShiftTimesAsync(id, request.StartTime, request.EndTime, request.ConfirmGap, RequestingUserId, User.IsInRole("Admin"), cancellationToken);

    private string RequestingUserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
}
