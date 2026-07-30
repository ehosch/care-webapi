namespace Care.WebApi.Infrastructure.Mailing;

public static class EmailTemplates
{
    public static string InviteEmail(string link) => $"""
        <p>You've been invited to join Care Coordination.</p>
        <p><a href="{link}">Click here to accept your invite and set up your account</a></p>
        <p>This link expires in 7 days. If you weren't expecting this, you can ignore this email.</p>
        """;

    public static string PasswordResetEmail(string link) => $"""
        <p>We received a request to reset your Care Coordination password.</p>
        <p><a href="{link}">Click here to reset your password</a></p>
        <p>If you didn't request this, you can ignore this email — your password won't change.</p>
        """;
}
