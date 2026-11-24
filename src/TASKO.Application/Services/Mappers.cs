using Tasko.Application.Contracts;
using Tasko.Domain;
using Tasko.Domain.Entities;

namespace Tasko.Application.Services;

internal static class Mappers
{
    public static UserSummaryResponse ToSummary(this User user) => new(
        user.Id,
        user.Name,
        user.Email,
        user.Roles.Select(r => r.Role).Distinct().OrderBy(r => r).ToList());

    public static MyProfileResponse ToMyProfile(this User user)
    {
        var profile = user.Profile;
        return new MyProfileResponse(
            user.Id,
            user.Name,
            user.Email,
            user.Roles.Select(r => r.Role).Distinct().OrderBy(r => r).ToList(),
            profile?.Presentation,
            profile?.Phone,
            profile?.City,
            profile?.Description,
            profile?.Neighborhood,
            profile?.ServiceRegion,
            profile?.ProfilePhotoUrl,
            user.Skills.Select(ToSkill).ToList(),
            profile?.UpdatedAt);
    }

    public static PublicProfileResponse ToPublicProfile(this User user, double? averageRating, int reviewCount)
    {
        var profile = user.Profile;
        return new PublicProfileResponse(
            user.Id,
            user.Name,
            profile?.Presentation,
            profile?.City,
            profile?.Description,
            profile?.Neighborhood,
            profile?.ServiceRegion,
            profile?.ProfilePhotoUrl,
            user.Skills.Select(ToSkill).ToList(),
            averageRating,
            reviewCount,
            user.CreatedAt);
    }

    private static SkillResponse ToSkill(UserSkill skill) =>
        new(skill.CategoryId, skill.Category?.Name ?? string.Empty, skill.SkillName);

    public static PublicationResponse ToResponse(this Publication publication) => new(
        publication.Id,
        publication.AuthorId,
        publication.Author?.Name ?? string.Empty,
        publication.CategoryId,
        publication.Category?.Name ?? string.Empty,
        publication.Type,
        publication.Title,
        publication.Description,
        publication.City,
        publication.Neighborhood,
        publication.ServiceRegion,
        publication.Status,
        publication.BudgetMin,
        publication.BudgetMax,
        publication.OfferedPrice,
        publication.Deadline,
        publication.Proposals.Count,
        publication.CreatedAt,
        publication.UpdatedAt,
        publication.ClosedAt);

    public static PublicationDetailResponse ToDetail(this Publication publication) => new(
        publication.ToResponse(),
        publication.StatusHistory
            .OrderBy(h => h.CreatedAt)
            .Select(h => new StatusHistoryResponse(h.OldStatus, h.NewStatus, h.ChangedBy, h.Note, h.CreatedAt))
            .ToList());

    public static ProposalResponse ToResponse(this Proposal proposal, double? proposerRating) => new(
        proposal.Id,
        proposal.PublicationId,
        proposal.Publication?.Title ?? string.Empty,
        proposal.ProposerId,
        proposal.Proposer?.Name ?? string.Empty,
        proposal.Proposer?.Profile?.City,
        proposerRating,
        proposal.Message,
        proposal.ProposedPrice,
        proposal.Status,
        proposal.CreatedAt,
        proposal.DecidedAt);

    public static EngagementResponse ToResponse(this Engagement engagement) => new(
        engagement.Id,
        engagement.PublicationId,
        engagement.Publication?.Title ?? string.Empty,
        engagement.Publication?.Type ?? default,
        engagement.ProposerId,
        engagement.Proposer?.Name ?? string.Empty,
        engagement.ReceiverId,
        engagement.Receiver?.Name ?? string.Empty,
        engagement.ProviderId,
        engagement.Provider?.Name ?? string.Empty,
        engagement.AgreedPrice,
        engagement.Status,
        engagement.ContactRevealedAt,
        engagement.StartedAt,
        engagement.CompletedAt,
        engagement.ContactRevealedAt is not null && engagement.Status != EngagementStatus.Cancelled);

    public static ReviewResponse ToResponse(this Review review) => new(
        review.Id,
        review.PublicationId,
        review.ReviewerId,
        review.Reviewer?.Name ?? string.Empty,
        review.ReviewedId,
        review.Rating,
        review.Comment,
        review.CreatedAt);

    public static NotificationResponse ToResponse(this Notification notification) => new(
        notification.Id,
        notification.Type,
        notification.Title,
        notification.Message,
        notification.ReferenceType,
        notification.ReferenceId,
        notification.ReadAt,
        notification.CreatedAt);

    public static ReportResponse ToResponse(this Report report) => new(
        report.Id,
        report.ReporterId,
        report.ReportedUserId,
        report.PublicationId,
        report.Reason,
        report.Description,
        report.Status,
        report.ModeratorId,
        report.ModerationNote,
        report.CreatedAt,
        report.ResolvedAt);
}

internal static class Pagination
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 50;

    public static (int Page, int PageSize) Normalize(int page, int pageSize)
    {
        var normalizedPage = page < 1 ? 1 : page;
        var normalizedSize = pageSize <= 0 ? DefaultPageSize : Math.Min(pageSize, MaxPageSize);
        return (normalizedPage, normalizedSize);
    }
}
