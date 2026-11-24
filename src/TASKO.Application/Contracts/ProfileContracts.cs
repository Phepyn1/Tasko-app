using Tasko.Domain;

namespace Tasko.Application.Contracts;

public sealed record UpdateProfileRequest(
    string? Presentation,
    string? Phone,
    string? City,
    string? Description,
    string? Neighborhood,
    string? ServiceRegion,
    string? ProfilePhotoUrl);

public sealed record SkillItem(Guid CategoryId, string SkillName);

public sealed record UpdateSkillsRequest(IReadOnlyList<SkillItem> Skills);

public sealed record SkillResponse(Guid CategoryId, string CategoryName, string SkillName);

/// <summary>Perfil do próprio usuário: único DTO que expõe telefone/e-mail.</summary>
public sealed record MyProfileResponse(
    Guid Id,
    string Name,
    string Email,
    IReadOnlyList<RoleType> Roles,
    string? Presentation,
    string? Phone,
    string? City,
    string? Description,
    string? Neighborhood,
    string? ServiceRegion,
    string? ProfilePhotoUrl,
    IReadOnlyList<SkillResponse> Skills,
    DateTimeOffset? UpdatedAt);

/// <summary>Perfil público (RF04/RF15): sem e-mail e sem telefone (RNF05/RN06).</summary>
public sealed record PublicProfileResponse(
    Guid Id,
    string Name,
    string? Presentation,
    string? City,
    string? Description,
    string? Neighborhood,
    string? ServiceRegion,
    string? ProfilePhotoUrl,
    IReadOnlyList<SkillResponse> Skills,
    double? AverageRating,
    int ReviewCount,
    DateTimeOffset MemberSince);
