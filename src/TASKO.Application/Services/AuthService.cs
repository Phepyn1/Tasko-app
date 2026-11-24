using Tasko.Application.Abstractions;
using Tasko.Application.Contracts;
using Tasko.Domain;
using Tasko.Domain.Entities;
using Tasko.Domain.Repositories;

namespace Tasko.Application.Services;

public sealed class AuthService(
    IUserRepository users,
    IRefreshTokenRepository refreshTokens,
    IPasswordResetTokenRepository resetTokens,
    IUnitOfWork unitOfWork,
    IPasswordHasher passwordHasher,
    ITokenService tokenService,
    IEmailSender emailSender,
    ICurrentUser currentUser,
    IClock clock,
    JwtOptions jwtOptions,
    AuthOptions authOptions,
    AuditService audit)
{
    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct)
    {
        var roles = (request.Roles ?? []).Distinct().ToList();
        if (roles.Count == 0)
        {
            throw DomainException.Validation("auth.roles_required", "Informe ao menos um papel: solicitante ou prestador.");
        }

        if (roles.Contains(RoleType.Moderator))
        {
            throw DomainException.Forbidden("auth.role_moderator", "O papel de moderador não pode ser solicitado no cadastro.");
        }

        if (roles.Any(r => r is not (RoleType.Requester or RoleType.Provider)))
        {
            throw DomainException.Validation("auth.role_invalid", "Papel inválido.");
        }

        PasswordPolicy.Validate(request.Password);

        var email = User.NormalizeEmail(request.Email);
        if (await users.EmailExistsAsync(email, ct))
        {
            throw DomainException.Conflict("auth.email_taken", "E-mail já cadastrado.");
        }

        var now = clock.UtcNow;
        var user = User.Create(request.Name, email, passwordHasher.Hash(request.Password), now);
        foreach (var role in roles)
        {
            user.AddRole(role, now);
        }

        user.EnsureProfile(now);
        await users.AddAsync(user, ct);

        var response = await IssueTokensAsync(user, now);
        audit.Record("auth.register", "user", user.Id, new { roles });
        await unitOfWork.SaveChangesAsync(ct);
        return response;
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var email = User.NormalizeEmail(request.Email);
        var user = await users.GetByEmailAsync(email, ct);
        if (user is null || !passwordHasher.Verify(request.Password ?? string.Empty, user.PasswordHash))
        {
            throw DomainException.Unauthorized("auth.invalid_credentials", "Credenciais inválidas.");
        }

        user.EnsureActive();
        var now = clock.UtcNow;
        var response = await IssueTokensAsync(user, now);
        audit.Record("auth.login", "user", user.Id);
        await unitOfWork.SaveChangesAsync(ct);
        return response;
    }

    public async Task<AuthResponse> RefreshAsync(RefreshRequest request, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var hash = tokenService.HashToken(request.RefreshToken ?? string.Empty);
        var stored = await refreshTokens.GetByHashAsync(hash, ct);
        if (stored is null || !stored.IsActive(now))
        {
            throw DomainException.Unauthorized("auth.invalid_refresh", "Refresh token inválido ou expirado.");
        }

        var user = await users.GetByIdAsync(stored.UserId, ct)
            ?? throw DomainException.Unauthorized("auth.invalid_refresh", "Refresh token inválido ou expirado.");
        user.EnsureActive();

        var opaque = tokenService.CreateOpaqueToken(TimeSpan.FromDays(jwtOptions.RefreshTokenDays));
        stored.Revoke(now, opaque.Hash);
        await refreshTokens.AddAsync(
            RefreshToken.Create(user.Id, opaque.Hash, opaque.ExpiresAt, now, currentUser.IpAddress), ct);

        var access = tokenService.CreateAccessToken(user);
        audit.Record("auth.refresh", "user", user.Id);
        await unitOfWork.SaveChangesAsync(ct);

        return new AuthResponse(access.Token, access.ExpiresAt, opaque.Token, user.ToSummary());
    }

    public async Task LogoutAsync(LogoutRequest request, CancellationToken ct)
    {
        var hash = tokenService.HashToken(request.RefreshToken ?? string.Empty);
        var stored = await refreshTokens.GetByHashAsync(hash, ct);
        if (stored is null)
        {
            return;
        }

        stored.Revoke(clock.UtcNow);
        audit.Record("auth.logout", "user", stored.UserId);
        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task<ForgotPasswordResponse> ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken ct)
    {
        var generic = new ForgotPasswordResponse(
            "Se o e-mail estiver cadastrado, enviaremos instruções de recuperação.",
            null);

        var email = User.NormalizeEmail(request.Email);
        var user = await users.GetByEmailAsync(email, ct);
        if (user is null || !user.IsActive)
        {
            return generic;
        }

        var now = clock.UtcNow;
        var opaque = tokenService.CreateOpaqueToken(TimeSpan.FromMinutes(authOptions.PasswordResetTokenMinutes));

        foreach (var active in await resetTokens.ListActiveByUserAsync(user.Id, ct))
        {
            active.Consume(now);
        }

        await resetTokens.AddAsync(
            PasswordResetToken.Create(user.Id, opaque.Hash, opaque.ExpiresAt, now), ct);

        await emailSender.SendAsync(
            user.Email,
            "TASKO - Recuperação de senha",
            $"Use o token abaixo para redefinir sua senha. Ele expira em {authOptions.PasswordResetTokenMinutes} minutos.{Environment.NewLine}{Environment.NewLine}{opaque.Token}",
            ct);

        audit.Record("auth.forgot_password", "user", user.Id);
        await unitOfWork.SaveChangesAsync(ct);

        return generic with { ResetToken = authOptions.ExposeSensitiveTokens ? opaque.Token : null };
    }

    public async Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct)
    {
        PasswordPolicy.Validate(request.NewPassword);

        var hash = tokenService.HashToken(request.Token ?? string.Empty);
        var stored = await resetTokens.GetByHashAsync(hash, ct);
        var now = clock.UtcNow;
        if (stored is null || !stored.IsUsable(now))
        {
            throw DomainException.Validation("auth.reset_invalid", "Token de recuperação inválido ou expirado.");
        }

        var user = await users.GetByIdAsync(stored.UserId, ct)
            ?? throw DomainException.Validation("auth.reset_invalid", "Token de recuperação inválido ou expirado.");

        user.UpdatePassword(passwordHasher.Hash(request.NewPassword!), now);
        stored.Consume(now);

        foreach (var token in await refreshTokens.ListActiveByUserAsync(user.Id, ct))
        {
            token.Revoke(now);
        }

        audit.Record("auth.password_reset", "user", user.Id);
        await unitOfWork.SaveChangesAsync(ct);
    }

    private async Task<AuthResponse> IssueTokensAsync(User user, DateTimeOffset now)
    {
        var access = tokenService.CreateAccessToken(user);
        var opaque = tokenService.CreateOpaqueToken(TimeSpan.FromDays(jwtOptions.RefreshTokenDays));

        await refreshTokens.AddAsync(
            RefreshToken.Create(user.Id, opaque.Hash, opaque.ExpiresAt, now, currentUser.IpAddress));

        return new AuthResponse(access.Token, access.ExpiresAt, opaque.Token, user.ToSummary());
    }
}
