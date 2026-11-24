namespace Tasko.Domain.Entities;

public sealed class Proposal
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid PublicationId { get; private set; }
    public Publication? Publication { get; private set; }
    public Guid ProposerId { get; private set; }
    public User? Proposer { get; private set; }
    public string Message { get; private set; } = string.Empty;
    public decimal? ProposedPrice { get; private set; }
    public ProposalStatus Status { get; private set; } = ProposalStatus.Pending;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset? DecidedAt { get; private set; }

    private Proposal()
    {
    }

    public static Proposal Create(Guid publicationId, Guid proposerId, string message, decimal? proposedPrice, DateTimeOffset now) => new()
    {
        PublicationId = Guard.RequiredId(publicationId, "Publicação", "proposal.publication_id"),
        ProposerId = Guard.RequiredId(proposerId, "Proponente", "proposal.proposer_id"),
        Message = Guard.Required(message, "Mensagem", 5, 2000, "proposal.message"),
        ProposedPrice = Guard.NonNegative(proposedPrice, "Preço proposto", "proposal.proposed_price"),
        Status = ProposalStatus.Pending,
        CreatedAt = now,
        UpdatedAt = now
    };

    public void Accept(DateTimeOffset now)
    {
        EnsurePending();
        Status = ProposalStatus.Accepted;
        DecidedAt = now;
        UpdatedAt = now;
    }

    public void Reject(DateTimeOffset now)
    {
        EnsurePending();
        Status = ProposalStatus.Rejected;
        DecidedAt = now;
        UpdatedAt = now;
    }

    public void Withdraw(DateTimeOffset now)
    {
        EnsurePending();
        Status = ProposalStatus.Withdrawn;
        DecidedAt = now;
        UpdatedAt = now;
    }

    public void Expire(DateTimeOffset now)
    {
        EnsurePending();
        Status = ProposalStatus.Expired;
        DecidedAt = now;
        UpdatedAt = now;
    }

    private void EnsurePending()
    {
        if (Status != ProposalStatus.Pending)
        {
            throw DomainException.Conflict("proposal.not_pending", $"A proposta já foi decidida (status '{Status}').");
        }
    }
}

/// <summary>
/// Formaliza a contratação após o aceite (RN05). Normaliza quem presta o serviço
/// independentemente de a publicação ser oferta ou solicitação.
/// </summary>
public sealed class Engagement
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid PublicationId { get; private set; }
    public Publication? Publication { get; private set; }
    public Guid ProposerId { get; private set; }
    public User? Proposer { get; private set; }
    public Guid ReceiverId { get; private set; }
    public User? Receiver { get; private set; }
    public Guid ProviderId { get; private set; }
    public User? Provider { get; private set; }
    public decimal? AgreedPrice { get; private set; }
    public EngagementStatus Status { get; private set; } = EngagementStatus.Active;
    public DateTimeOffset? ContactRevealedAt { get; private set; }
    public DateTimeOffset StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private Engagement()
    {
    }

    public static Engagement Create(Publication publication, Proposal proposal, DateTimeOffset now)
    {
        if (publication.Status != PublicationStatus.InNegotiation)
        {
            throw DomainException.Conflict("engagement.publication_state", "A publicação precisa estar em negociação para gerar contratação.");
        }

        if (proposal.Status != ProposalStatus.Accepted)
        {
            throw DomainException.Conflict("engagement.proposal_state", "A proposta precisa estar aceita para gerar contratação.");
        }

        if (proposal.PublicationId != publication.Id)
        {
            throw DomainException.Validation("engagement.publication_mismatch", "Proposta não pertence à publicação.");
        }

        var providerId = publication.Type == PublicationType.Offer
            ? publication.AuthorId
            : proposal.ProposerId;

        return new Engagement
        {
            PublicationId = publication.Id,
            ProposerId = proposal.ProposerId,
            ReceiverId = publication.AuthorId,
            ProviderId = providerId,
            AgreedPrice = proposal.ProposedPrice,
            Status = EngagementStatus.Active,
            ContactRevealedAt = now,
            StartedAt = now,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public void Complete(DateTimeOffset now)
    {
        if (Status != EngagementStatus.Active)
        {
            throw DomainException.Conflict("engagement.not_active", $"Contratação não está ativa (status '{Status}').");
        }

        Status = EngagementStatus.Completed;
        CompletedAt = now;
        UpdatedAt = now;
    }

    public void Cancel(DateTimeOffset now)
    {
        if (Status != EngagementStatus.Active)
        {
            throw DomainException.Conflict("engagement.not_active", $"Contratação não está ativa (status '{Status}').");
        }

        Status = EngagementStatus.Cancelled;
        UpdatedAt = now;
    }

    public void RevealContact(DateTimeOffset now) => ContactRevealedAt ??= now;

    public bool IsParticipant(Guid userId) => userId == ProposerId || userId == ReceiverId;

    public void EnsureParticipant(Guid userId)
    {
        if (!IsParticipant(userId))
        {
            throw DomainException.Forbidden("engagement.forbidden", "Apenas os participantes da negociação podem acessar este recurso.");
        }
    }

    public Guid CounterpartOf(Guid userId) => userId == ProposerId ? ReceiverId : ProposerId;
}

public sealed class Review
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid PublicationId { get; private set; }
    public Publication? Publication { get; private set; }
    public Guid EngagementId { get; private set; }
    public Engagement? Engagement { get; private set; }
    public Guid ReviewerId { get; private set; }
    public User? Reviewer { get; private set; }
    public Guid ReviewedId { get; private set; }
    public User? Reviewed { get; private set; }
    public int Rating { get; private set; }
    public string? Comment { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private Review()
    {
    }

    /// <summary>RN07 (só após conclusão), RN08 (anti-autoavaliação) e participação no contrato.</summary>
    public static Review Create(
        Publication publication,
        Engagement engagement,
        Guid reviewerId,
        Guid reviewedId,
        int rating,
        string? comment,
        DateTimeOffset now)
    {
        if (publication.Status != PublicationStatus.Completed)
        {
            throw DomainException.Conflict("review.publication_not_completed", "Avaliações só são permitidas após a conclusão do serviço.");
        }

        if (engagement.Status != EngagementStatus.Completed)
        {
            throw DomainException.Conflict("review.engagement_not_completed", "Avaliações só são permitidas após a conclusão do serviço.");
        }

        if (engagement.PublicationId != publication.Id)
        {
            throw DomainException.Validation("review.engagement_mismatch", "Contratação não pertence à publicação informada.");
        }

        if (!engagement.IsParticipant(reviewerId))
        {
            throw DomainException.Forbidden("review.not_participant", "Apenas participantes da contratação podem avaliar.");
        }

        if (reviewerId == reviewedId)
        {
            throw DomainException.Forbidden("review.self", "Não é permitido avaliar a própria conta.");
        }

        if (reviewedId != engagement.CounterpartOf(reviewerId))
        {
            throw DomainException.Forbidden("review.target", "A avaliação deve ser direcionada à contraparte da contratação.");
        }

        if (rating is < 1 or > 5)
        {
            throw DomainException.Validation("review.rating", "A nota deve estar entre 1 e 5.");
        }

        return new Review
        {
            PublicationId = publication.Id,
            EngagementId = engagement.Id,
            ReviewerId = reviewerId,
            ReviewedId = reviewedId,
            Rating = rating,
            Comment = Guard.Optional(comment, "Comentário", 1000, "review.comment"),
            CreatedAt = now
        };
    }
}
