using Tasko.Domain;
using Tasko.Domain.Entities;

namespace Tasko.Application.Abstractions;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string passwordHash);
}

public interface ITokenService
{
    AccessTokenResult CreateAccessToken(User user);

    /// <summary>Gera token opaco (refresh/reset) e o hash que deve ser persistido.</summary>
    OpaqueTokenResult CreateOpaqueToken(TimeSpan lifetime);

    string HashToken(string token);
}

public sealed record AccessTokenResult(string Token, DateTimeOffset ExpiresAt);

public sealed record OpaqueTokenResult(string Token, string Hash, DateTimeOffset ExpiresAt);

public interface IEmailSender
{
    Task SendAsync(string to, string subject, string body, CancellationToken ct = default);
}

public interface ICurrentUser
{
    Guid? UserId { get; }
    string? IpAddress { get; }
    bool IsInRole(RoleType role);
}

public static class CurrentUserExtensions
{
    public static Guid RequireUserId(this ICurrentUser currentUser) =>
        currentUser.UserId ?? throw DomainException.Unauthorized("auth.required", "Autenticação obrigatória.");

    public static void RequireModerator(this ICurrentUser currentUser)
    {
        if (!currentUser.IsInRole(RoleType.Moderator))
        {
            throw DomainException.Forbidden("auth.moderator", "Ação restrita à moderação.");
        }
    }
}

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "tasko-api";
    public string Audience { get; set; } = "tasko-app";
    public string SigningKey { get; set; } = string.Empty;
    public int AccessTokenMinutes { get; set; } = 15;
    public int RefreshTokenDays { get; set; } = 7;
}

public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    /// <summary>
    /// Expõe o token de reset na resposta. Usar apenas em Development/testes —
    /// em produção o token vai somente por e-mail.
    /// </summary>
    public bool ExposeSensitiveTokens { get; set; }

    public int PasswordResetTokenMinutes { get; set; } = 60;
}

internal static class PasswordPolicy
{
    public static void Validate(string? password)
    {
        var value = password ?? string.Empty;
        if (value.Length < 8 || value.Length > 128)
        {
            throw DomainException.Validation("password.length", "A senha deve ter entre 8 e 128 caracteres.");
        }

        if (!value.Any(char.IsLetter) || !value.Any(char.IsDigit))
        {
            throw DomainException.Validation("password.strength", "A senha deve conter ao menos uma letra e um número.");
        }
    }
}
