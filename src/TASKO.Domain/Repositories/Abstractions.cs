using Tasko.Domain.Entities;

namespace Tasko.Domain.Repositories;

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Total);

public sealed record PublicationQuery(
    PublicationType? Type,
    Guid? CategoryId,
    string? City,
    string? ServiceRegion,
    string? Term,
    Guid? AuthorId,
    PublicationStatus? Status,
    int Page,
    int PageSize);

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<User?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<bool> EmailExistsAsync(string email, CancellationToken ct = default);
    Task AddAsync(User user, CancellationToken ct = default);

    /// <summary>
    /// Substitui as habilidades do usuário. A gravação é feita pelo repositório porque
    /// entidades com chave gerada no cliente precisam ser adicionadas explicitamente ao
    /// <c>DbSet</c> (o rastreamento por coleção de navegação as marcaria como Modified).
    /// </summary>
    Task ReplaceSkillsAsync(
        Guid userId,
        IReadOnlyList<(Guid CategoryId, string Name)> skills,
        DateTimeOffset now,
        CancellationToken ct = default);
}

public interface ICategoryRepository
{
    Task<IReadOnlyList<Category>> ListAsync(CancellationToken ct = default);
    Task<Category?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<bool> ExistsAsync(Guid id, CancellationToken ct = default);
    Task AddRangeAsync(IEnumerable<Category> categories, CancellationToken ct = default);
}

public interface IPublicationRepository
{
    Task<Publication?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Publication?> GetDetailAsync(Guid id, CancellationToken ct = default);

    /// <summary>Carrega a publicação com lock de linha (PostgreSQL) para o aceite atômico.</summary>
    Task<Publication?> GetForUpdateAsync(Guid id, CancellationToken ct = default);

    Task<PagedResult<Publication>> SearchAsync(PublicationQuery query, CancellationToken ct = default);
    Task<PagedResult<Publication>> ListByAuthorAsync(Guid authorId, int page, int pageSize, CancellationToken ct = default);
    Task AddAsync(Publication publication, CancellationToken ct = default);
    void AddStatusHistory(PublicationStatusHistory history);
}

public interface IProposalRepository
{
    Task<Proposal?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Proposal>> ListByPublicationAsync(Guid publicationId, CancellationToken ct = default);
    Task<IReadOnlyList<Proposal>> ListPendingByPublicationAsync(Guid publicationId, CancellationToken ct = default);
    Task<PagedResult<Proposal>> ListByProposerAsync(Guid proposerId, int page, int pageSize, CancellationToken ct = default);
    Task<bool> HasPendingAsync(Guid publicationId, Guid proposerId, CancellationToken ct = default);
    Task AddAsync(Proposal proposal, CancellationToken ct = default);
}

public interface IEngagementRepository
{
    Task<Engagement?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Engagement?> GetByPublicationAsync(Guid publicationId, CancellationToken ct = default);
    Task<PagedResult<Engagement>> ListByUserAsync(Guid userId, int page, int pageSize, CancellationToken ct = default);
    Task<bool> AnyActiveByPublicationAsync(Guid publicationId, CancellationToken ct = default);
    Task AddAsync(Engagement engagement, CancellationToken ct = default);
}

public interface IReviewRepository
{
    Task<bool> ExistsAsync(Guid publicationId, Guid reviewerId, CancellationToken ct = default);
    Task<PagedResult<Review>> ListByReviewedUserAsync(Guid userId, int page, int pageSize, CancellationToken ct = default);
    Task<PagedResult<Review>> ListByPublicationAsync(Guid publicationId, int page, int pageSize, CancellationToken ct = default);
    Task<(double? Average, int Count)> GetRatingAsync(Guid userId, CancellationToken ct = default);
    Task AddAsync(Review review, CancellationToken ct = default);
}

public interface INotificationRepository
{
    Task<Notification?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<PagedResult<Notification>> ListByUserAsync(Guid userId, bool unreadOnly, int page, int pageSize, CancellationToken ct = default);
    Task<int> CountUnreadAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<Notification>> ListUnreadByUserAsync(Guid userId, CancellationToken ct = default);
    Task AddAsync(Notification notification, CancellationToken ct = default);
}

public interface IReportRepository
{
    Task<Report?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<PagedResult<Report>> ListAsync(ReportStatus? status, int page, int pageSize, CancellationToken ct = default);
    Task AddAsync(Report report, CancellationToken ct = default);
}

public interface IRefreshTokenRepository
{
    Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken ct = default);
    Task<IReadOnlyList<RefreshToken>> ListActiveByUserAsync(Guid userId, CancellationToken ct = default);
    Task AddAsync(RefreshToken token, CancellationToken ct = default);
}

public interface IPasswordResetTokenRepository
{
    Task<PasswordResetToken?> GetByHashAsync(string tokenHash, CancellationToken ct = default);
    Task<IReadOnlyList<PasswordResetToken>> ListActiveByUserAsync(Guid userId, CancellationToken ct = default);
    Task AddAsync(PasswordResetToken token, CancellationToken ct = default);
}

public interface IActivityLogRepository
{
    void Add(ActivityLog log);
}

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);
    Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken ct = default);
}

public interface IUnitOfWorkTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken ct = default);
    Task RollbackAsync(CancellationToken ct = default);
}
