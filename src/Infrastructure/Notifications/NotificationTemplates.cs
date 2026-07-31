namespace Care.WebApi.Infrastructure.Notifications;

public static class NotificationTemplates
{
    public static string ShiftAssignedEmail(DateOnly date, TimeSpan startTime, TimeSpan endTime) => $"""
        <p>You're now assigned to a shift on {date:dddd, MMMM d}, {FormatTime(startTime)}–{FormatTime(endTime)}.</p>
        """;

    public static string ShiftAssignedSms(DateOnly date, TimeSpan startTime, TimeSpan endTime) =>
        $"Care Coordination: you're now assigned to a shift on {date:ddd, MMM d}, {FormatTime(startTime)}-{FormatTime(endTime)}.";

    public static string ReplacementRequestedEmail(DateOnly date, TimeSpan startTime, TimeSpan endTime, string requestedByName, string? reason, string link) => $"""
        <p>{requestedByName} requested a replacement for their shift on {date:dddd, MMMM d}, {FormatTime(startTime)}–{FormatTime(endTime)}{(string.IsNullOrEmpty(reason) ? "" : $" — \"{reason}\"")}.</p>
        <p><a href="{link}">Open the Replacement Requests page</a> if you're able to cover it.</p>
        """;

    public static string ReplacementRequestedSms(DateOnly date, TimeSpan startTime, TimeSpan endTime, string requestedByName, string link) =>
        $"Care Coordination: {requestedByName} needs a replacement for their shift on {date:ddd, MMM d}, {FormatTime(startTime)}-{FormatTime(endTime)}. {link}";

    public static string ReplacementClaimedEmail(DateOnly date, TimeSpan startTime, TimeSpan endTime, string claimedByName) => $"""
        <p>{claimedByName} has covered your shift on {date:dddd, MMMM d}, {FormatTime(startTime)}–{FormatTime(endTime)}. You're released from it.</p>
        """;

    public static string ReplacementClaimedSms(DateOnly date, TimeSpan startTime, TimeSpan endTime, string claimedByName) =>
        $"Care Coordination: {claimedByName} covered your shift on {date:ddd, MMM d}, {FormatTime(startTime)}-{FormatTime(endTime)}.";

    public static string DocumentUploadedEmail(string title, string category, string uploadedByName) => $"""
        <p>{uploadedByName} uploaded a new document: "{title}" ({category}).</p>
        """;

    public static string DocumentUploadedSms(string title, string uploadedByName) =>
        $"Care Coordination: {uploadedByName} uploaded a new document — \"{title}\".";

    public static string ShiftRemovedEmail(DateOnly date, TimeSpan startTime, TimeSpan endTime) => $"""
        <p>Your shift on {date:dddd, MMMM d} from {FormatTime(startTime)}–{FormatTime(endTime)} was removed.</p>
        """;

    public static string ShiftRemovedSms(DateOnly date, TimeSpan startTime, TimeSpan endTime) =>
        $"Care Coordination: your shift on {date:ddd, MMM d} from {FormatTime(startTime)}-{FormatTime(endTime)} was removed.";

    public static string ShiftBoundaryChangedEmail(DateOnly date, TimeSpan newStartTime, TimeSpan newEndTime) => $"""
        <p>Your shift on {date:dddd, MMMM d} was adjusted — it's now {FormatTime(newStartTime)}–{FormatTime(newEndTime)}.</p>
        """;

    public static string ShiftBoundaryChangedSms(DateOnly date, TimeSpan newStartTime, TimeSpan newEndTime) =>
        $"Care Coordination: your shift on {date:ddd, MMM d} is now {FormatTime(newStartTime)}-{FormatTime(newEndTime)}.";

    private static string FormatTime(TimeSpan time) => DateTime.Today.Add(time).ToString("h:mm tt");
}
