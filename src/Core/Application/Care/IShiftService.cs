namespace Care.WebApi.Application.Care;

public interface IShiftService
{
    Task<List<ShiftDto>> GetShiftsAsync(DateOnly weekStart, CancellationToken cancellationToken);

    Task AssignShiftAsync(Guid shiftId, string? userId, CancellationToken cancellationToken);
}
