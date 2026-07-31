namespace Care.WebApi.Domain.Care;

// Open is never produced anymore — uncovered time is the absence of a Shift
// row, not a row with this status. Kept at ordinal 0 (rather than removed)
// because EF stores this enum as a plain int with no HasConversion; removing
// it would renumber Assigned/ReplacementRequested and silently reinterpret
// every existing row.
public enum ShiftStatus
{
    Open,
    Assigned,
    ReplacementRequested,
    Confirmed
}
