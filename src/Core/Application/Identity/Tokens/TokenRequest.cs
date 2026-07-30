namespace Care.WebApi.Application.Identity.Tokens;

public record TokenRequest(string Email, string Password);

public record RefreshTokenRequest(string Token, string RefreshToken);

public record TokenResponse(string Token, string RefreshToken, DateTime RefreshTokenExpiryTime);
