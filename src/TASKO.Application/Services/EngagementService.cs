using Tasko.Application.Abstractions;
using Tasko.Application.Contracts;
using Tasko.Domain;
using Tasko.Domain.Entities;
using Tasko.Domain.Repositories;

namespace Tasko.Application.Services;

public sealed class EngagementService(
    IEngagementRepository engagements,
    IPublicationRepository publications,
    INotificationRepository notifications,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IClock clock,
    AuditService audit)
{
    public async Task<PagedResponse<EngagementResponse>> ListMineAsync(int page, int pageSize, CancellationToken ct)
    {
        var userId = currentUser.RequireUserId();
        var (normalizedPage, normalizedSize) = Pagination.Normalize(page, pageSize);
        var result = await engagements.ListByUserAsync(userId, normalizedPage, normalizedSize, ct);

        return new PagedResponse<EngagementResponse>(
            result.Items.Select(e => e.ToResponse()).ToList(),
            normalizedPage,
            normalizedSize,
            result.Total);
    }

    public async Task<EngagementResponse> GetAsync(Guid id, CancellationToken ct)
    {
        var engagement = await LoadParticipantAsync(id, ct);
        return engagement.ToResponse();
    }

    /// <summary>RF13/RN06: contato liberado somente após o aceite e apenas para os participantes.</summary>
    public async Task<ContactResponse> GetContactAsync(Guid id, CancellationToken ct)
    {
        var engagement = await LoadParticipantAsync(id, ct);

        if (engagement.ContactRevealedAt is null || engagement.Status == EngagementStatus.Cancelled)
        {
            throw DomainException.Forbidden(
                "engagement.contact_unavailable",
                "O contato só é liberado após o aceite da proposta.");
        }

        var proposer = engagement.Proposer
            ?? throw DomainException.NotFound("engagement.proposer_missing", "Proponente não encontrado.");
        var receiver = engagement.Receiver
            ?? throw DomainException.NotFound("engagement.receiver_missing", "Autor não encontrado.");

        audit.Record("engagement.contact_revealed", "engagement", engagement.Id);
        await unitOfWork.SaveChangesAsync(ct);

        return new ContactResponse(
            engagement.Id,
            new ContactParticipant(proposer.Id, proposer.Name, proposer.Email, proposer.Profile?.Phone),
            new ContactParticipant(receiver.Id, receiver.Name, receiver.Email, receiver.Profile?.Phone));
    }

    /// <summary>RN05: confirmação explícita da combinação (InNegotiation → Agreed).</summary>
    public async Task<EngagementResponse> ConfirmAsync(Guid id, CancellationToken ct)
    {
        var userId = currentUser.RequireUserId();
        var engagement = await LoadParticipantAsync(id, ct);
        var publication = engagement.Publication
            ?? throw DomainException.NotFound("publication.not_found", "Publicação não encontrada.");

        if (publication.AuthorId != userId)
        {
            throw DomainException.Forbidden("engagement.not_author", "Apenas o autor da publicação pode confirmar a combinação.");
        }

        var now = clock.UtcNow;
        publication.MarkAgreed(now);
        publications.AddStatusHistory(PublicationStatusHistory.Create(
            publication.Id, userId, PublicationStatus.InNegotiation, PublicationStatus.Agreed, "Combinação confirmada", now));

        await notifications.AddAsync(Notification.Create(
            engagement.CounterpartOf(userId),
            NotificationType.PublicationStatusChanged,
            "Combinação confirmada",
            $"A combinação de \"{publication.Title}\" foi confirmada.",
            "engagement",
            engagement.Id,
            now), ct);

        audit.Record("engagement.confirm", "engagement", engagement.Id);
        await unitOfWork.SaveChangesAsync(ct);
        return engagement.ToResponse();
    }

    /// <summary>Conclusão unilateral pelo autor da publicação (default aprovado do MVP).</summary>
    public async Task<EngagementResponse> CompleteAsync(Guid id, CancellationToken ct)
    {
        var userId = currentUser.RequireUserId();
        var engagement = await LoadParticipantAsync(id, ct);
        var publication = engagement.Publication
            ?? throw DomainException.NotFound("publication.not_found", "Publicação não encontrada.");

        if (publication.AuthorId != userId)
        {
            throw DomainException.Forbidden("engagement.not_author", "Apenas o autor da publicação pode concluir o serviço.");
        }

        var now = clock.UtcNow;
        engagement.Complete(now);
        publication.Complete(now);
        publications.AddStatusHistory(PublicationStatusHistory.Create(
            publication.Id, userId, PublicationStatus.Agreed, PublicationStatus.Completed, "Serviço concluído", now));

        await notifications.AddAsync(Notification.Create(
            engagement.CounterpartOf(userId),
            NotificationType.EngagementCompleted,
            "Serviço concluído",
            $"A contratação de \"{publication.Title}\" foi concluída. Avalie a experiência.",
            "engagement",
            engagement.Id,
            now), ct);

        audit.Record("engagement.complete", "engagement", engagement.Id);
        await unitOfWork.SaveChangesAsync(ct);

        var saved = await engagements.GetByIdAsync(engagement.Id, ct);
        return saved!.ToResponse();
    }

    public async Task<EngagementResponse> CancelAsync(Guid id, CancellationToken ct)
    {
        var userId = currentUser.RequireUserId();
        var engagement = await LoadParticipantAsync(id, ct);
        var publication = engagement.Publication
            ?? throw DomainException.NotFound("publication.not_found", "Publicação não encontrada.");

        var now = clock.UtcNow;
        engagement.Cancel(now);

        if (publication.Status == PublicationStatus.InNegotiation)
        {
            publication.Reopen(now);
            publications.AddStatusHistory(PublicationStatusHistory.Create(
                publication.Id, userId, PublicationStatus.InNegotiation, PublicationStatus.Open, "Contratação cancelada", now));
        }

        await notifications.AddAsync(Notification.Create(
            engagement.CounterpartOf(userId),
            NotificationType.PublicationStatusChanged,
            "Contratação cancelada",
            $"A contratação de \"{publication.Title}\" foi cancelada.",
            "engagement",
            engagement.Id,
            now), ct);

        audit.Record("engagement.cancel", "engagement", engagement.Id);
        await unitOfWork.SaveChangesAsync(ct);

        var saved = await engagements.GetByIdAsync(engagement.Id, ct);
        return saved!.ToResponse();
    }

    private async Task<Engagement> LoadParticipantAsync(Guid id, CancellationToken ct)
    {
        var userId = currentUser.RequireUserId();
        var engagement = await engagements.GetByIdAsync(id, ct)
            ?? throw DomainException.NotFound("engagement.not_found", "Contratação não encontrada.");

        engagement.EnsureParticipant(userId);
        return engagement;
    }
}
