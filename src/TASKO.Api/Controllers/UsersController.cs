using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tasko.Application.Contracts;
using Tasko.Application.Services;

namespace Tasko.Api.Controllers;

[ApiController]
[Route("api/v1/users")]
public sealed class UsersController(ProfileService profiles, ReviewService reviews) : ControllerBase
{
    [HttpGet("me")]
    [Authorize]
    public Task<MyProfileResponse> GetMe(CancellationToken ct) => profiles.GetMineAsync(ct);

    [HttpPut("me/profile")]
    [Authorize]
    public Task<MyProfileResponse> UpdateProfile(UpdateProfileRequest request, CancellationToken ct) =>
        profiles.UpdateMineAsync(request, ct);

    [HttpPut("me/skills")]
    [Authorize]
    public Task<MyProfileResponse> UpdateSkills(UpdateSkillsRequest request, CancellationToken ct) =>
        profiles.ReplaceSkillsAsync(request, ct);

    /// <summary>Perfil público: sem e-mail e sem telefone (RNF05).</summary>
    [HttpGet("{id:guid}/profile")]
    [AllowAnonymous]
    public Task<PublicProfileResponse> GetProfile(Guid id, CancellationToken ct) =>
        profiles.GetPublicAsync(id, ct);

    [HttpGet("{id:guid}/reviews")]
    [AllowAnonymous]
    public Task<PagedResponse<ReviewResponse>> GetReviews(
        Guid id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default) =>
        reviews.ListByUserAsync(id, page, pageSize, ct);
}
