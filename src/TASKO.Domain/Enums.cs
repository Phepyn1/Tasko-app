namespace Tasko.Domain;

/// <summary>Papéis de conta (RN01: uma conta pode acumular solicitante + prestador).</summary>
public enum RoleType
{
    Requester,
    Provider,
    Moderator
}

/// <summary>Tipo da publicação: oferta de serviço ou solicitação de demanda.</summary>
public enum PublicationType
{
    Offer,
    Request
}

public enum PublicationStatus
{
    Open,
    InNegotiation,
    Agreed,
    Completed,
    Cancelled,
    Hidden
}

public enum ProposalStatus
{
    Pending,
    Accepted,
    Rejected,
    Withdrawn,
    Expired
}

public enum EngagementStatus
{
    Active,
    Completed,
    Cancelled,
    Disputed
}

public enum ReportStatus
{
    Pending,
    UnderReview,
    Resolved,
    Dismissed
}

public enum ReportReason
{
    Spam,
    Fraud,
    Offensive,
    Inappropriate,
    Other
}

public enum NotificationType
{
    ProposalReceived,
    ProposalAccepted,
    ProposalRejected,
    PublicationStatusChanged,
    EngagementCompleted,
    ReviewReceived,
    ReportResolved
}

public enum ModerationAction
{
    Dismiss,
    Hide
}
