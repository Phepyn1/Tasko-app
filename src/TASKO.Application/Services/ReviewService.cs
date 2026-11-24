using Tasko.Application.Abstractions;
using Tasko.Application.Contracts;
using Tasko.Domain;
using Tasko.Domain.Entities;
using Tasko.Domain.Repositories;

namespace Tasko.Application.Services;

public sealed class ReviewService(
    IReviewRepository reviews,
    IPublicationRepository publications,
    IEngagementRepository engagements,
    IUserRepository users,
    INotificationRepository notifications,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IClock clock,
    AuditService audit)
{
    /// <summary>RF15 + RN07 + RN08: só após conclusão, só participantes, sem autoavaliação.</summary>
    public async Task<ReviewResponse> CreateAsync(Guid publicationId, CreateReviewRequest request, CancellationToken ct)
    {
        var userId = currentUser.RequireUserId();
        var now = clock.UtcNow;

        var publication = await publications.GetDetailAsync(publicationId, ct)
            ?? throw DomainException.NotFound("publication.not_found", "Publicação não encontrada.");

        var engagement = await engagements.GetByPublicationAsync(publicationId, ct)
            ?? throw DomainException.Conflict("review.no_engagement", "Não há contratação vinculada a esta publicação.");

        if (await reviews.ExistsAsync(publicationId, userId, ct))
        {
            throw DomainException.Conflict("review.duplicate", "Você já avaliou esta publicação.");
        }

        var review = Review.Create(
            publication,
            engagement,
            userId,
            request.ReviewedUserId,
            request.Rating,
            request.Comment,
            now);

        await reviews.AddAsync(review, ct);

        var reviewer = await users.GetByIdAsync(userId, ct);
        await notifications.AddAsync(Notification.Create(
            review.ReviewedId,
            NotificationType.ReviewReceived,
            "Nova avaliação recebida",
            $"{reviewer?.Name ?? "Um usuário"} avaliou você com nota {review.Rating}.",
            "user",
            review.ReviewedId,
            now), ct);

        audit.Record("review.create", "review", review.Id, new { rating = review.Rating });
        await unitOfWork.SaveChangesAsync(ct);

        return new ReviewResponse(
            review.Id,
            review.PublicationId,
            review.ReviewerId,
            reviewer?.Name ?? string.Empty,
            review.ReviewedId,
            review.Rating,
            review.Comment,
            review.CreatedAt);
    }

    public async Task<PagedResponse<ReviewResponse>> ListByUserAsync(Guid userId, int page, int pageSize, CancellationToken ct)
    {
        var (normalizedPage, normalizedSize) = Pagination.Normalize(page, pageSize);
        var result = await reviews.ListByReviewedUserAsync(userId, normalizedPage, normalizedSize, ct);

        return new PagedResponse<ReviewResponse>(
            result.Items.Select(r => r.ToResponse()).ToList(),
            normalizedPage,
            normalizedSize,
            result.Total);
    }

    public async Task<PagedResponse<ReviewResponse>> ListByPublicationAsync(Guid publicationId, int page, int pageSize, CancellationToken ct)
    {
        var (normalizedPage, normalizedSize) = Pagination.Normalize(page, pageSize);
        var result = await reviews.ListByPublicationAsync(publicationId, normalizedPage, normalizedSize, ct);

        return new PagedResponse<ReviewResponse>(
            result.Items.Select(r => r.ToResponse()).ToList(),
            normalizedPage,
            normalizedSize,
            result.Total);
    }
}
