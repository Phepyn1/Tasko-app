using Tasko.Application.Abstractions;
using Tasko.Application.Contracts;
using Tasko.Domain;
using Tasko.Domain.Entities;
using Tasko.Domain.Repositories;

namespace Tasko.Application.Services;

public sealed class ReportService(
    IReportRepository reports,
    IPublicationRepository publications,
    IUserRepository users,
    INotificationRepository notifications,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IClock clock,
    AuditService audit)
{
    /// <summary>RF16/RN10: apenas enfileira para moderação, sem ocultação automática.</summary>
    public async Task<ReportResponse> CreateAsync(CreateReportRequest request, CancellationToken ct)
    {
        var reporterId = currentUser.RequireUserId();

        if (request.PublicationId is not null)
        {
            _ = await publications.GetByIdAsync(request.PublicationId.Value, ct)
                ?? throw DomainException.NotFound("publication.not_found", "Publicação denunciada não encontrada.");
        }

        if (request.ReportedUserId is not null)
        {
            _ = await users.GetByIdAsync(request.ReportedUserId.Value, ct)
                ?? throw DomainException.NotFound("user.not_found", "Perfil denunciado não encontrado.");
        }

        var report = Report.Create(
            reporterId,
            request.ReportedUserId,
            request.PublicationId,
            request.Reason,
            request.Description,
            clock.UtcNow);

        await reports.AddAsync(report, ct);
        audit.Record("report.create", "report", report.Id, new { reason = report.Reason.ToString() });
        await unitOfWork.SaveChangesAsync(ct);
        return report.ToResponse();
    }

    public async Task<PagedResponse<ReportResponse>> ListAsync(
        ReportStatus? status,
        int page,
        int pageSize,
        CancellationToken ct)
    {
        currentUser.RequireModerator();

        var (normalizedPage, normalizedSize) = Pagination.Normalize(page, pageSize);
        var result = await reports.ListAsync(status, normalizedPage, normalizedSize, ct);

        return new PagedResponse<ReportResponse>(
            result.Items.Select(r => r.ToResponse()).ToList(),
            normalizedPage,
            normalizedSize,
            result.Total);
    }

    /// <summary>RF17: revisão humana. Ocultação só ocorre por decisão explícita do moderador.</summary>
    public async Task<ReportResponse> ResolveAsync(Guid id, ResolveReportRequest request, CancellationToken ct)
    {
        var moderatorId = currentUser.RequireUserId();
        currentUser.RequireModerator();

        var report = await reports.GetByIdAsync(id, ct)
            ?? throw DomainException.NotFound("report.not_found", "Denúncia não encontrada.");

        var now = clock.UtcNow;

        if (request.Action == ModerationAction.Hide)
        {
            if (report.PublicationId is null)
            {
                throw DomainException.Validation("report.publication_required", "Ocultação exige uma publicação denunciada.");
            }

            var note = request.Note?.Trim();
            if (string.IsNullOrWhiteSpace(note) || note.Length < 5)
            {
                throw DomainException.Validation("report.note_required", "Informe a justificativa da ocultação (mínimo 5 caracteres).");
            }

            var publication = await publications.GetByIdAsync(report.PublicationId.Value, ct)
                ?? throw DomainException.NotFound("publication.not_found", "Publicação denunciada não encontrada.");

            var previous = publication.Status;
            publication.Hide(note, now);
            publications.AddStatusHistory(PublicationStatusHistory.Create(
                publication.Id, moderatorId, previous, PublicationStatus.Hidden, note, now));

            await notifications.AddAsync(Notification.Create(
                publication.AuthorId,
                NotificationType.ReportResolved,
                "Publicação ocultada",
                $"Sua publicação \"{publication.Title}\" foi ocultada pela moderação: {note}",
                "publication",
                publication.Id,
                now), ct);

            report.Resolve(moderatorId, note, now);
        }
        else
        {
            report.Dismiss(moderatorId, request.Note, now);
        }

        audit.Record("report.resolve", "report", report.Id, new { action = request.Action.ToString() });
        await unitOfWork.SaveChangesAsync(ct);
        return report.ToResponse();
    }
}
