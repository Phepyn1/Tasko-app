namespace Tasko.Domain.Entities;

public sealed class Report
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid ReporterId { get; private set; }
    public User? Reporter { get; private set; }
    public Guid? ReportedUserId { get; private set; }
    public User? ReportedUser { get; private set; }
    public Guid? PublicationId { get; private set; }
    public Publication? Publication { get; private set; }
    public ReportReason Reason { get; private set; }
    public string? Description { get; private set; }
    public ReportStatus Status { get; private set; } = ReportStatus.Pending;
    public Guid? ModeratorId { get; private set; }
    public User? Moderator { get; private set; }
    public string? ModerationNote { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset? ResolvedAt { get; private set; }

    private Report()
    {
    }

    /// <summary>RN10: apenas enfileira para moderação; nenhuma ocultação automática.</summary>
    public static Report Create(
        Guid reporterId,
        Guid? reportedUserId,
        Guid? publicationId,
        ReportReason reason,
        string? description,
        DateTimeOffset now)
    {
        if (reportedUserId is null && publicationId is null)
        {
            throw DomainException.Validation("report.target_required", "Informe o perfil ou a publicação denunciada.");
        }

        if (reportedUserId is not null && reportedUserId == reporterId)
        {
            throw DomainException.Forbidden("report.self", "Não é permitido denunciar a própria conta.");
        }

        return new Report
        {
            ReporterId = Guard.RequiredId(reporterId, "Denunciante", "report.reporter_id"),
            ReportedUserId = reportedUserId,
            PublicationId = publicationId,
            Reason = reason,
            Description = Guard.Optional(description, "Descrição", 1000, "report.description"),
            Status = ReportStatus.Pending,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public void AssignModerator(Guid moderatorId, DateTimeOffset now)
    {
        if (Status is ReportStatus.Resolved or ReportStatus.Dismissed)
        {
            throw DomainException.Conflict("report.already_closed", "Denúncia já encerrada.");
        }

        ModeratorId = moderatorId;
        Status = ReportStatus.UnderReview;
        UpdatedAt = now;
    }

    public void Resolve(Guid moderatorId, string note, DateTimeOffset now)
    {
        if (Status is ReportStatus.Resolved or ReportStatus.Dismissed)
        {
            throw DomainException.Conflict("report.already_closed", "Denúncia já encerrada.");
        }

        ModeratorId = moderatorId;
        ModerationNote = Guard.Required(note, "Nota de moderação", 5, 500, "report.moderation_note");
        Status = ReportStatus.Resolved;
        ResolvedAt = now;
        UpdatedAt = now;
    }

    public void Dismiss(Guid moderatorId, string? note, DateTimeOffset now)
    {
        if (Status is ReportStatus.Resolved or ReportStatus.Dismissed)
        {
            throw DomainException.Conflict("report.already_closed", "Denúncia já encerrada.");
        }

        ModeratorId = moderatorId;
        ModerationNote = Guard.Optional(note, "Nota de moderação", 500, "report.moderation_note");
        Status = ReportStatus.Dismissed;
        ResolvedAt = now;
        UpdatedAt = now;
    }
}

public sealed class Notification
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid UserId { get; private set; }
    public User? User { get; private set; }
    public NotificationType Type { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Message { get; private set; } = string.Empty;
    public string? ReferenceType { get; private set; }
    public Guid? ReferenceId { get; private set; }
    public DateTimeOffset? ReadAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private Notification()
    {
    }

    public static Notification Create(
        Guid userId,
        NotificationType type,
        string title,
        string message,
        string? referenceType,
        Guid? referenceId,
        DateTimeOffset now) => new()
    {
        UserId = Guard.RequiredId(userId, "Usuário", "notification.user_id"),
        Type = type,
        Title = Guard.Required(title, "Título", 3, 150, "notification.title"),
        Message = Guard.Required(message, "Mensagem", 3, 500, "notification.message"),
        ReferenceType = Guard.Optional(referenceType, "Tipo de referência", 60, "notification.reference_type"),
        ReferenceId = referenceId,
        CreatedAt = now
    };

    public void MarkAsRead(DateTimeOffset now) => ReadAt ??= now;
}

public sealed class ActivityLog
{
    public long Id { get; private set; }
    public Guid? ActorId { get; private set; }
    public User? Actor { get; private set; }
    public string Action { get; private set; } = string.Empty;
    public string EntityType { get; private set; } = string.Empty;
    public string? EntityId { get; private set; }
    public string? Metadata { get; private set; }
    public string? IpAddress { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private ActivityLog()
    {
    }

    public static ActivityLog Create(
        Guid? actorId,
        string action,
        string entityType,
        string? entityId,
        string? metadata,
        string? ipAddress,
        DateTimeOffset now) => new()
    {
        ActorId = actorId,
        Action = Guard.Required(action, "Ação", 3, 100, "activity_log.action"),
        EntityType = Guard.Required(entityType, "Tipo de entidade", 2, 60, "activity_log.entity_type"),
        EntityId = Guard.Optional(entityId, "Id da entidade", 64, "activity_log.entity_id"),
        Metadata = metadata,
        IpAddress = Guard.Optional(ipAddress, "IP", 45, "activity_log.ip"),
        CreatedAt = now
    };
}
