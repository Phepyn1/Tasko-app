using Tasko.Application.Abstractions;
using Tasko.Application.Contracts;
using Tasko.Domain;
using Tasko.Domain.Entities;
using Tasko.Domain.Repositories;

namespace Tasko.Application.Services;

public sealed class ProfileService(
    IUserRepository users,
    ICategoryRepository categories,
    IReviewRepository reviews,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IClock clock,
    AuditService audit)
{
    public async Task<MyProfileResponse> GetMineAsync(CancellationToken ct)
    {
        var user = await LoadCurrentAsync(ct);
        return user.ToMyProfile();
    }

    public async Task<MyProfileResponse> UpdateMineAsync(UpdateProfileRequest request, CancellationToken ct)
    {
        var user = await LoadCurrentAsync(ct);
        user.EnsureActive();

        var now = clock.UtcNow;
        user.EnsureProfile(now).Update(
            request.Presentation,
            request.Phone,
            request.City,
            request.Description,
            request.Neighborhood,
            request.ServiceRegion,
            request.ProfilePhotoUrl,
            now);

        audit.Record("profile.update", "user", user.Id);
        await unitOfWork.SaveChangesAsync(ct);
        return user.ToMyProfile();
    }

    public async Task<MyProfileResponse> ReplaceSkillsAsync(UpdateSkillsRequest request, CancellationToken ct)
    {
        var user = await LoadCurrentAsync(ct);
        user.EnsureActive();

        var skills = request.Skills ?? [];
        foreach (var skill in skills)
        {
            if (!await categories.ExistsAsync(skill.CategoryId, ct))
            {
                throw DomainException.NotFound("category.not_found", "Categoria não encontrada.");
            }
        }

        await users.ReplaceSkillsAsync(
            user.Id,
            skills.Select(s => (s.CategoryId, s.SkillName)).ToList(),
            clock.UtcNow,
            ct);
        audit.Record("profile.skills_update", "user", user.Id, new { count = skills.Count });
        await unitOfWork.SaveChangesAsync(ct);

        var reloaded = await users.GetByIdAsync(user.Id, ct) ?? user;
        return reloaded.ToMyProfile();
    }

    public async Task<PublicProfileResponse> GetPublicAsync(Guid userId, CancellationToken ct)
    {
        var user = await users.GetByIdAsync(userId, ct)
            ?? throw DomainException.NotFound("user.not_found", "Perfil não encontrado.");

        var (average, count) = await reviews.GetRatingAsync(userId, ct);
        return user.ToPublicProfile(average, count);
    }

    private async Task<User> LoadCurrentAsync(CancellationToken ct)
    {
        var userId = currentUser.RequireUserId();
        var user = await users.GetByIdAsync(userId, ct)
            ?? throw DomainException.Unauthorized("user.not_found", "Usuário autenticado não encontrado.");
        user.EnsureActive();
        return user;
    }
}
