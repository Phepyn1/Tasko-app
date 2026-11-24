using Tasko.Domain;

namespace Tasko.Application.Contracts;

public sealed record CreatePublicationRequest(
    PublicationType Type,
    Guid CategoryId,
    string Title,
    string Description,
    string City,
    string? Neighborhood,
    string ServiceRegion,
    decimal? BudgetMin,
    decimal? BudgetMax,
    decimal? OfferedPrice,
    DateTimeOffset? Deadline);

public sealed record UpdatePublicationRequest(
    Guid CategoryId,
    string Title,
    string Description,
    string City,
    string? Neighborhood,
    string ServiceRegion,
    decimal? BudgetMin,
    decimal? BudgetMax,
    decimal? OfferedPrice,
    DateTimeOffset? Deadline);

public sealed record PublicationResponse(
    Guid Id,
    Guid AuthorId,
    string AuthorName,
    Guid CategoryId,
    string CategoryName,
    PublicationType Type,
    string Title,
    string Description,
    string City,
    string? Neighborhood,
    string ServiceRegion,
    PublicationStatus Status,
    decimal? BudgetMin,
    decimal? BudgetMax,
    decimal? OfferedPrice,
    DateTimeOffset? Deadline,
    int ProposalCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? ClosedAt);

public sealed record PublicationDetailResponse(
    PublicationResponse Publication,
    IReadOnlyList<StatusHistoryResponse> StatusHistory);

public sealed record StatusHistoryResponse(
    PublicationStatus OldStatus,
    PublicationStatus NewStatus,
    Guid? ChangedBy,
    string? Note,
    DateTimeOffset CreatedAt);

public sealed record CategoryResponse(Guid Id, string Name, string? Description);
