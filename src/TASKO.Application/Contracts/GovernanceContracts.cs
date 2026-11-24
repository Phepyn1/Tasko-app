using Tasko.Domain;

namespace Tasko.Application.Contracts;

public sealed record NotificationResponse(
    Guid Id,
    NotificationType Type,
    string Title,
    string Message,
    string? ReferenceType,
    Guid? ReferenceId,
    DateTimeOffset? ReadAt,
    DateTimeOffset CreatedAt);

public sealed record UnreadCountResponse(int Count);

public sealed record CreateReportRequest(
    Guid? ReportedUserId,
    Guid? PublicationId,
    ReportReason Reason,
    string? Description);

public sealed record ReportResponse(
    Guid Id,
    Guid ReporterId,
    Guid? ReportedUserId,
    Guid? PublicationId,
    ReportReason Reason,
    string? Description,
    ReportStatus Status,
    Guid? ModeratorId,
    string? ModerationNote,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ResolvedAt);

public sealed record ResolveReportRequest(ModerationAction Action, string? Note);
