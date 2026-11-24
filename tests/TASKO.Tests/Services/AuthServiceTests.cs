using Microsoft.EntityFrameworkCore;
using Tasko.Application.Contracts;
using Tasko.Domain;

namespace Tasko.Tests.Services;

public sealed class AuthServiceTests
{
    private static RegisterRequest RegisterRequest(string email = "fulano@teste.com", params RoleType[] roles) =>
        new("Fulano de Tal", email, "Senha123", roles.Length == 0 ? [RoleType.Requester] : roles);

    [Fact]
    public async Task Register_should_create_account_with_roles_profile_and_tokens()
    {
        await using var h = new TestHarness();

        var response = await h.Auth.RegisterAsync(
            RegisterRequest(roles: [RoleType.Requester, RoleType.Provider]),
            CancellationToken.None);

        Assert.NotEmpty(response.AccessToken);
        Assert.NotEmpty(response.RefreshToken);
        Assert.Contains(RoleType.Requester, response.User.Roles);
        Assert.Contains(RoleType.Provider, response.User.Roles);
        Assert.True(await h.Db.UserProfiles.AnyAsync(p => p.UserId == response.User.Id));
        Assert.True(await h.Db.RefreshTokens.AnyAsync(t => t.UserId == response.User.Id));
    }

    [Fact]
    public async Task Register_should_reject_duplicate_email()
    {
        await using var h = new TestHarness();
        await h.Auth.RegisterAsync(RegisterRequest(), CancellationToken.None);

        var exception = await Assert.ThrowsAsync<DomainException>(
            () => h.Auth.RegisterAsync(RegisterRequest(), CancellationToken.None));

        Assert.Equal("auth.email_taken", exception.Code);
    }

    [Fact]
    public async Task Register_should_reject_moderator_role()
    {
        await using var h = new TestHarness();

        var exception = await Assert.ThrowsAsync<DomainException>(
            () => h.Auth.RegisterAsync(RegisterRequest(roles: [RoleType.Moderator]), CancellationToken.None));

        Assert.Equal("auth.role_moderator", exception.Code);
    }

    [Fact]
    public async Task Login_should_reject_invalid_password()
    {
        await using var h = new TestHarness();
        await h.Auth.RegisterAsync(RegisterRequest(), CancellationToken.None);

        var exception = await Assert.ThrowsAsync<DomainException>(
            () => h.Auth.LoginAsync(new LoginRequest("fulano@teste.com", "Errada123"), CancellationToken.None));

        Assert.Equal("auth.invalid_credentials", exception.Code);
    }

    [Fact]
    public async Task Refresh_should_rotate_and_revoke_previous_token()
    {
        await using var h = new TestHarness();
        var first = await h.Auth.RegisterAsync(RegisterRequest(), CancellationToken.None);

        var second = await h.Auth.RefreshAsync(new RefreshRequest(first.RefreshToken), CancellationToken.None);
        Assert.NotEqual(first.RefreshToken, second.RefreshToken);

        var exception = await Assert.ThrowsAsync<DomainException>(
            () => h.Auth.RefreshAsync(new RefreshRequest(first.RefreshToken), CancellationToken.None));

        Assert.Equal("auth.invalid_refresh", exception.Code);
    }

    [Fact]
    public async Task Forgot_and_reset_password_flow_should_work()
    {
        await using var h = new TestHarness();
        await h.Auth.RegisterAsync(RegisterRequest(), CancellationToken.None);

        var forgot = await h.Auth.ForgotPasswordAsync(
            new ForgotPasswordRequest("fulano@teste.com"),
            CancellationToken.None);
        Assert.NotNull(forgot.ResetToken);

        await h.Auth.ResetPasswordAsync(
            new ResetPasswordRequest(forgot.ResetToken!, "NovaSenha123"),
            CancellationToken.None);

        var login = await h.Auth.LoginAsync(
            new LoginRequest("fulano@teste.com", "NovaSenha123"),
            CancellationToken.None);
        Assert.NotEmpty(login.AccessToken);

        await Assert.ThrowsAsync<DomainException>(
            () => h.Auth.LoginAsync(new LoginRequest("fulano@teste.com", "Senha123"), CancellationToken.None));

        await Assert.ThrowsAsync<DomainException>(
            () => h.Auth.ResetPasswordAsync(
                new ResetPasswordRequest(forgot.ResetToken!, "OutraSenha123"),
                CancellationToken.None));
    }

    [Fact]
    public async Task Forgot_password_should_not_reveal_whether_email_exists()
    {
        await using var h = new TestHarness();

        var response = await h.Auth.ForgotPasswordAsync(
            new ForgotPasswordRequest("naoexiste@teste.com"),
            CancellationToken.None);

        Assert.Null(response.ResetToken);
        Assert.Empty(h.Email.Sent);
    }
}
