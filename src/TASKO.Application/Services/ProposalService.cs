using Tasko.Application.Abstractions;
using Tasko.Application.Contracts;
using Tasko.Domain;
using Tasko.Domain.Entities;
using Tasko.Domain.Repositories;

namespace Tasko.Application.Services;

public sealed class ProposalService(
    IProposalRepository proposals,
    IPublicationRepository publications,
    IEngagementRepository engagements,
    IUserRepository users,
    IReviewRepository reviews,
    INotificationRepository notifications,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IClock clock,
    AuditService audit)
{
    /// <summary>RF10 + RN03: publicação aberta, sem auto-proposta, papel exigido por tipo.</summary>
    public async Task<ProposalResponse> CreateAsync(Guid publicationId, CreateProposalRequest request, CancellationToken ct)
    {
        var userId = currentUser.RequireUserId();
        var now = clock.UtcNow;

        var publication = await publications.GetByIdAsync(publicationId, ct)
            ?? throw DomainException.NotFound("publication.not_found", "Publicação não encontrada.");

        publication.EnsureAcceptsProposals();

        if (publication.AuthorId == userId)
        {
            throw DomainException.Forbidden("proposal.self", "Não é possível enviar proposta para a própria publicação.");
        }

        var user = await users.GetByIdAsync(userId, ct)
            ?? throw DomainException.Unauthorized("user.not_found", "Usuário autenticado não encontrado.");
        user.EnsureActive();

        if (publication.Type == PublicationType.Request && !user.HasRole(RoleType.Provider))
        {
            throw DomainException.Forbidden("proposal.provider_required", "Apenas prestadores podem propor em solicitações de demanda.");
        }

        if (await proposals.HasPendingAsync(publicationId, userId, ct))
        {
            throw DomainException.Conflict("proposal.duplicate", "Você já possui uma proposta pendente nesta publicação.");
        }

        var proposal = Proposal.Create(publicationId, userId, request.Message, request.ProposedPrice, now);
        await proposals.AddAsync(proposal, ct);

        await notifications.AddAsync(Notification.Create(
            publication.AuthorId,
            NotificationType.ProposalReceived,
            "Nova proposta recebida",
            $"{user.Name} enviou uma proposta para \"{publication.Title}\".",
            "publication",
            publication.Id,
            now), ct);

        audit.Record("proposal.create", "proposal", proposal.Id, new { publicationId });
        await unitOfWork.SaveChangesAsync(ct);

        return proposal.ToResponse(null);
    }

    /// <summary>RF11: propostas recebidas, com dados públicos do proponente (sem contato).</summary>
    public async Task<IReadOnlyList<ProposalResponse>> ListForPublicationAsync(Guid publicationId, CancellationToken ct)
    {
        var userId = currentUser.RequireUserId();
        var publication = await publications.GetByIdAsync(publicationId, ct)
            ?? throw DomainException.NotFound("publication.not_found", "Publicação não encontrada.");

        if (publication.AuthorId != userId)
        {
            throw DomainException.Forbidden("proposal.not_author", "Apenas o autor da publicação pode listar as propostas recebidas.");
        }

        var items = await proposals.ListByPublicationAsync(publicationId, ct);
        var result = new List<ProposalResponse>(items.Count);
        foreach (var proposal in items)
        {
            var (average, _) = await reviews.GetRatingAsync(proposal.ProposerId, ct);
            result.Add(proposal.ToResponse(average));
        }

        return result;
    }

    public async Task<PagedResponse<ProposalResponse>> ListMineAsync(int page, int pageSize, CancellationToken ct)
    {
        var userId = currentUser.RequireUserId();
        var (normalizedPage, normalizedSize) = Pagination.Normalize(page, pageSize);
        var result = await proposals.ListByProposerAsync(userId, normalizedPage, normalizedSize, ct);

        return new PagedResponse<ProposalResponse>(
            result.Items.Select(p => p.ToResponse(null)).ToList(),
            normalizedPage,
            normalizedSize,
            result.Total);
    }

    /// <summary>
    /// RF12/RN04/RN05: aceite atômico — proposta aceita, demais recusadas, publicação em negociação,
    /// histórico, contratação criada, contato liberado e notificação enviada.
    /// </summary>
    public async Task<EngagementResponse> AcceptAsync(Guid proposalId, CancellationToken ct)
    {
        var userId = currentUser.RequireUserId();
        var now = clock.UtcNow;

        await using var transaction = await unitOfWork.BeginTransactionAsync(ct);

        var proposal = await proposals.GetByIdAsync(proposalId, ct)
            ?? throw DomainException.NotFound("proposal.not_found", "Proposta não encontrada.");

        var publication = await publications.GetForUpdateAsync(proposal.PublicationId, ct)
            ?? throw DomainException.NotFound("publication.not_found", "Publicação não encontrada.");

        if (publication.AuthorId != userId)
        {
            throw DomainException.Forbidden("proposal.not_author", "Apenas o autor da publicação pode aceitar propostas.");
        }

        publication.EnsureAcceptsProposals();
        proposal.Accept(now);

        foreach (var other in await proposals.ListPendingByPublicationAsync(publication.Id, ct))
        {
            if (other.Id != proposal.Id)
            {
                other.Reject(now);
            }
        }

        publication.StartNegotiation(now);
        publications.AddStatusHistory(PublicationStatusHistory.Create(
            publication.Id,
            userId,
            PublicationStatus.Open,
            PublicationStatus.InNegotiation,
            "Proposta aceita",
            now));

        var engagement = Engagement.Create(publication, proposal, now);
        await engagements.AddAsync(engagement, ct);

        await notifications.AddAsync(Notification.Create(
            proposal.ProposerId,
            NotificationType.ProposalAccepted,
            "Proposta aceita",
            $"Sua proposta para \"{publication.Title}\" foi aceita. O contato direto foi liberado.",
            "engagement",
            engagement.Id,
            now), ct);

        audit.Record("proposal.accept", "proposal", proposal.Id, new { engagementId = engagement.Id });
        await unitOfWork.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        var saved = await engagements.GetByIdAsync(engagement.Id, ct);
        return saved!.ToResponse();
    }

    public async Task<ProposalResponse> RejectAsync(Guid proposalId, CancellationToken ct)
    {
        var userId = currentUser.RequireUserId();
        var now = clock.UtcNow;

        var proposal = await proposals.GetByIdAsync(proposalId, ct)
            ?? throw DomainException.NotFound("proposal.not_found", "Proposta não encontrada.");

        if (proposal.Publication?.AuthorId != userId)
        {
            throw DomainException.Forbidden("proposal.not_author", "Apenas o autor da publicação pode recusar propostas.");
        }

        proposal.Reject(now);

        await notifications.AddAsync(Notification.Create(
            proposal.ProposerId,
            NotificationType.ProposalRejected,
            "Proposta recusada",
            $"Sua proposta para \"{proposal.Publication.Title}\" foi recusada.",
            "publication",
            proposal.PublicationId,
            now), ct);

        audit.Record("proposal.reject", "proposal", proposal.Id);
        await unitOfWork.SaveChangesAsync(ct);
        return proposal.ToResponse(null);
    }

    public async Task<ProposalResponse> WithdrawAsync(Guid proposalId, CancellationToken ct)
    {
        var userId = currentUser.RequireUserId();
        var proposal = await proposals.GetByIdAsync(proposalId, ct)
            ?? throw DomainException.NotFound("proposal.not_found", "Proposta não encontrada.");

        if (proposal.ProposerId != userId)
        {
            throw DomainException.Forbidden("proposal.not_proposer", "Apenas o autor da proposta pode retirá-la.");
        }

        proposal.Withdraw(clock.UtcNow);
        audit.Record("proposal.withdraw", "proposal", proposal.Id);
        await unitOfWork.SaveChangesAsync(ct);
        return proposal.ToResponse(null);
    }
}
