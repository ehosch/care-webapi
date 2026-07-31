using Care.WebApi.Domain.Care;

namespace Care.WebApi.Infrastructure.Notifications;

public static class NotificationTemplates
{
    public static string ShiftAssignedEmail(DateOnly date, ShiftType shiftType) => $"""
        <p>You're now assigned to the {shiftType} shift on {date:dddd, MMMM d}.</p>
        """;

    public static string ShiftAssignedSms(DateOnly date, ShiftType shiftType) =>
        $"Care Coordination: you're now assigned to the {shiftType} shift on {date:ddd, MMM d}.";

    public static string ReplacementRequestedEmail(DateOnly date, ShiftType shiftType, string requestedByName, string? reason) => $"""
        <p>{requestedByName} requested a replacement for the {shiftType} shift on {date:dddd, MMMM d}{(string.IsNullOrEmpty(reason) ? "" : $" — \"{reason}\"")}.</p>
        <p>Open the Replacement Requests page if you're able to cover it.</p>
        """;

    public static string ReplacementRequestedSms(DateOnly date, ShiftType shiftType, string requestedByName) =>
        $"Care Coordination: {requestedByName} needs a replacement for the {shiftType} shift on {date:ddd, MMM d}.";

    public static string ReplacementClaimedEmail(DateOnly date, ShiftType shiftType, string claimedByName) => $"""
        <p>{claimedByName} has covered your {shiftType} shift on {date:dddd, MMMM d}. You're released from it.</p>
        """;

    public static string ReplacementClaimedSms(DateOnly date, ShiftType shiftType, string claimedByName) =>
        $"Care Coordination: {claimedByName} covered your {shiftType} shift on {date:ddd, MMM d}.";

    public static string DocumentUploadedEmail(string title, string category, string uploadedByName) => $"""
        <p>{uploadedByName} uploaded a new document: "{title}" ({category}).</p>
        """;

    public static string DocumentUploadedSms(string title, string uploadedByName) =>
        $"Care Coordination: {uploadedByName} uploaded a new document — \"{title}\".";
}
