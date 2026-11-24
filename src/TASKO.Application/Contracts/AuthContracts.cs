using Tasko.Domain;

namespace Tasko.Application.Contracts;

public sealed record RegisterRequest(string Name, string Email, string Password, IReadOnlyList<RoleType> Roles);

public sealed record LoginRequest(string Email, string Password);

public sealed record RefreshRequest(string RefreshToken);

public sealed record LogoutRequest(string RefreshToken);

public sealed record ForgotPasswordRequest(string Email);

public sealed record ForgotPasswordResponse(string Message, string? ResetToken);

public sealed record ResetPasswordRequest(string Token, string NewPassword);

public sealed record UserSummaryResponse(Guid Id, string Name, string Email, IReadOnlyList<RoleType> Roles);

public sealed record AuthResponse(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    UserSummaryResponse User);
