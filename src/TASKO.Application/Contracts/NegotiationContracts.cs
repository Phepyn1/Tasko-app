using Tasko.Domain;

namespace Tasko.Application.Contracts;

public sealed record CreateProposalRequest(string Message, decimal? ProposedPrice);

public sealed record ProposalResponse(
    Guid Id,
    Guid PublicationId,
    string PublicationTitle,
    Guid ProposerId,
    string ProposerName,
    string? ProposerCity,
    double? ProposerRating,
    string Message,
    decimal? ProposedPrice,
    ProposalStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? DecidedAt);

public sealed record EngagementResponse(
    Guid Id,
    Guid PublicationId,
    string PublicationTitle,
    PublicationType PublicationType,
    Guid ProposerId,
    string ProposerName,
    Guid ReceiverId,
    string ReceiverName,
    Guid ProviderId,
    string ProviderName,
    decimal? AgreedPrice,
    EngagementStatus Status,
    DateTimeOffset? ContactRevealedAt,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    bool ContactAvailable);

public sealed record ContactParticipant(Guid UserId, string Name, string Email, string? Phone);

/// <summary>RN06: retornado apenas aos dois participantes da negociação aceita.</summary>
public sealed record ContactResponse(Guid EngagementId, ContactParticipant Proposer, ContactParticipant Receiver);

public sealed record CreateReviewRequest(Guid ReviewedUserId, int Rating, string? Comment);

public sealed record ReviewResponse(
    Guid Id,
    Guid PublicationId,
    Guid ReviewerId,
    string ReviewerName,
    Guid ReviewedId,
    int Rating,
    string? Comment,
    DateTimeOffset CreatedAt);
