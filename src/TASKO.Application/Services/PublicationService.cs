using Tasko.Application.Abstractions;
using Tasko.Application.Contracts;
using Tasko.Domain;
using Tasko.Domain.Entities;
using Tasko.Domain.Repositories;

namespace Tasko.Application.Services;

public sealed class PublicationService(
    IPublicationRepository publications,
    ICategoryRepository categories,
    IEngagementRepository engagements,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IClock clock,
    AuditService audit)
{
    public async Task<PublicationResponse> CreateAsync(CreatePublicationRequest request, CancellationToken ct)
    {
        var authorId = currentUser.RequireUserId();
        if (!await categories.ExistsAsync(request.CategoryId, ct))
        {
            throw DomainException.NotFound("category.not_found", "Categoria não encontrada.");
        }

        var input = new PublicationInput(
            request.Type,
            request.CategoryId,
            request.Title,
            request.Description,
            request.City,
            request.Neighborhood,
            request.ServiceRegion,
            request.BudgetMin,
            request.BudgetMax,
            request.OfferedPrice,
            request.Deadline);

        var publication = Publication.Create(authorId, input, clock.UtcNow);
        await publications.AddAsync(publication, ct);
        audit.Record("publication.create", "publication", publication.Id, new { type = publication.Type.ToString() });
        await unitOfWork.SaveChangesAsync(ct);

        var created = await publications.GetByIdAsync(publication.Id, ct) ?? publication;
        return created.ToResponse();
    }

    public async Task<PublicationResponse> UpdateAsync(Guid id, UpdatePublicationRequest request, CancellationToken ct)
    {
        var userId = currentUser.RequireUserId();
        var publication = await publications.GetByIdAsync(id, ct)
            ?? throw DomainException.NotFound("publication.not_found", "Publicação não encontrada.");

        EnsureAuthor(publication, userId);

        if (!await categories.ExistsAsync(request.CategoryId, ct))
        {
            throw DomainException.NotFound("category.not_found", "Categoria não encontrada.");
        }

        var hasActiveEngagement = await engagements.AnyActiveByPublicationAsync(id, ct);
        var input = new PublicationInput(
            publication.Type,
            request.CategoryId,
            request.Title,
            request.Description,
            request.City,
            request.Neighborhood,
            request.ServiceRegion,
            request.BudgetMin,
            request.BudgetMax,
            request.OfferedPrice,
            request.Deadline);

        publication.Update(input, hasActiveEngagement, clock.UtcNow);
        audit.Record("publication.update", "publication", publication.Id);
        await unitOfWork.SaveChangesAsync(ct);

        var updated = await publications.GetByIdAsync(id, ct) ?? publication;
        return updated.ToResponse();
    }

    /// <summary>RN09: encerrar só sem contratação ativa.</summary>
    public async Task<PublicationResponse> CancelAsync(Guid id, CancellationToken ct)
    {
        var userId = currentUser.RequireUserId();
        var publication = await publications.GetByIdAsync(id, ct)
            ?? throw DomainException.NotFound("publication.not_found", "Publicação não encontrada.");

        EnsureAuthor(publication, userId);
        var previous = publication.Status;
        publication.Cancel(clock.UtcNow);

        publications.AddStatusHistory(PublicationStatusHistory.Create(
            publication.Id, userId, previous, PublicationStatus.Cancelled, "Encerrada pelo autor", clock.UtcNow));

        audit.Record("publication.cancel", "publication", publication.Id);
        await unitOfWork.SaveChangesAsync(ct);
        return publication.ToResponse();
    }

    public async Task<PublicationDetailResponse> GetAsync(Guid id, CancellationToken ct)
    {
        var publication = await publications.GetDetailAsync(id, ct)
            ?? throw DomainException.NotFound("publication.not_found", "Publicação não encontrada.");

        var userId = currentUser.UserId;
        if (publication.Status == PublicationStatus.Hidden &&
            publication.AuthorId != userId &&
            !currentUser.IsInRole(RoleType.Moderator))
        {
            throw DomainException.NotFound("publication.not_found", "Publicação não encontrada.");
        }

        return publication.ToDetail();
    }

    public async Task<PagedResponse<PublicationResponse>> SearchAsync(
        PublicationType? type,
        Guid? categoryId,
        string? city,
        string? serviceRegion,
        string? term,
        PublicationStatus? status,
        int page,
        int pageSize,
        CancellationToken ct)
    {
        var (normalizedPage, normalizedSize) = Pagination.Normalize(page, pageSize);
        var query = new PublicationQuery(
            type,
            categoryId,
            city,
            serviceRegion,
            term,
            null,
            status ?? PublicationStatus.Open,
            normalizedPage,
            normalizedSize);

        var result = await publications.SearchAsync(query, ct);
        return new PagedResponse<PublicationResponse>(
            result.Items.Select(p => p.ToResponse()).ToList(),
            normalizedPage,
            normalizedSize,
            result.Total);
    }

    public async Task<PagedResponse<PublicationResponse>> ListMineAsync(int page, int pageSize, CancellationToken ct)
    {
        var userId = currentUser.RequireUserId();
        var (normalizedPage, normalizedSize) = Pagination.Normalize(page, pageSize);
        var result = await publications.ListByAuthorAsync(userId, normalizedPage, normalizedSize, ct);

        return new PagedResponse<PublicationResponse>(
            result.Items.Select(p => p.ToResponse()).ToList(),
            normalizedPage,
            normalizedSize,
            result.Total);
    }

    private static void EnsureAuthor(Publication publication, Guid userId)
    {
        if (publication.AuthorId != userId)
        {
            throw DomainException.Forbidden("publication.not_author", "Apenas o autor pode alterar esta publicação.");
        }
    }
}
