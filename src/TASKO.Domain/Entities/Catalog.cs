namespace Tasko.Domain.Entities;

public sealed class Category
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private Category()
    {
    }

    public static Category Create(string name, string? description, DateTimeOffset now) => new()
    {
        Name = Guard.Required(name, "Nome da categoria", 2, 80, "category.name"),
        Description = Guard.Optional(description, "Descrição", 500, "category.description"),
        CreatedAt = now
    };
}

public sealed record PublicationInput(
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

/// <summary>
/// Publicação unificada (RF05/RF06). Invariantes: RN02 (campos obrigatórios),
/// RN05 (máquina de estados) e RN09 (trava de edição).
/// </summary>
public sealed class Publication
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid AuthorId { get; private set; }
    public User? Author { get; private set; }
    public Guid CategoryId { get; private set; }
    public Category? Category { get; private set; }
    public PublicationType Type { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public string City { get; private set; } = string.Empty;
    public string? Neighborhood { get; private set; }
    public string ServiceRegion { get; private set; } = string.Empty;
    public PublicationStatus Status { get; private set; } = PublicationStatus.Open;
    public decimal? BudgetMin { get; private set; }
    public decimal? BudgetMax { get; private set; }
    public decimal? OfferedPrice { get; private set; }
    public DateTimeOffset? Deadline { get; private set; }
    public string? ModerationNote { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset? ClosedAt { get; private set; }

    public List<Proposal> Proposals { get; private set; } = [];
    public List<PublicationStatusHistory> StatusHistory { get; private set; } = [];

    private Publication()
    {
    }

    public static Publication Create(Guid authorId, PublicationInput input, DateTimeOffset now)
    {
        var normalized = Normalize(input, now);

        return new Publication
        {
            AuthorId = Guard.RequiredId(authorId, "Autor", "publication.author_id"),
            CategoryId = normalized.CategoryId,
            Type = normalized.Type,
            Title = normalized.Title,
            Description = normalized.Description,
            City = normalized.City,
            Neighborhood = normalized.Neighborhood,
            ServiceRegion = normalized.ServiceRegion,
            BudgetMin = normalized.BudgetMin,
            BudgetMax = normalized.BudgetMax,
            OfferedPrice = normalized.OfferedPrice,
            Deadline = normalized.Deadline,
            Status = PublicationStatus.Open,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public void Update(PublicationInput input, bool hasActiveEngagement, DateTimeOffset now)
    {
        EnsureEditable(hasActiveEngagement);

        var normalized = Normalize(input, now);
        if (normalized.Type != Type)
        {
            throw DomainException.Conflict("publication.type_immutable", "O tipo da publicação não pode ser alterado.");
        }

        CategoryId = normalized.CategoryId;
        Title = normalized.Title;
        Description = normalized.Description;
        City = normalized.City;
        Neighborhood = normalized.Neighborhood;
        ServiceRegion = normalized.ServiceRegion;
        BudgetMin = normalized.BudgetMin;
        BudgetMax = normalized.BudgetMax;
        OfferedPrice = normalized.OfferedPrice;
        Deadline = normalized.Deadline;
        UpdatedAt = now;
    }

    /// <summary>RN09: sem contratação ativa e apenas em publicações abertas.</summary>
    public void EnsureEditable(bool hasActiveEngagement)
    {
        if (Status != PublicationStatus.Open)
        {
            throw DomainException.Conflict("publication.not_editable", "Apenas publicações com status aberto podem ser alteradas.");
        }

        if (hasActiveEngagement)
        {
            throw DomainException.Conflict("publication.active_engagement", "Existe contratação ativa em andamento para esta publicação.");
        }
    }

    public void EnsureAcceptsProposals()
    {
        if (Status != PublicationStatus.Open)
        {
            throw DomainException.Conflict("publication.not_open", "A publicação não está aberta para propostas.");
        }
    }

    public void StartNegotiation(DateTimeOffset now)
    {
        if (Status != PublicationStatus.Open)
        {
            throw DomainException.Conflict("publication.invalid_transition", $"Não é possível iniciar negociação a partir de '{Status}'.");
        }

        Status = PublicationStatus.InNegotiation;
        UpdatedAt = now;
    }

    public void MarkAgreed(DateTimeOffset now)
    {
        if (Status != PublicationStatus.InNegotiation)
        {
            throw DomainException.Conflict("publication.invalid_transition", $"Não é possível confirmar a combinação a partir de '{Status}'.");
        }

        Status = PublicationStatus.Agreed;
        UpdatedAt = now;
    }

    public void Complete(DateTimeOffset now)
    {
        if (Status != PublicationStatus.Agreed)
        {
            throw DomainException.Conflict("publication.invalid_transition", $"Não é possível concluir a partir de '{Status}'.");
        }

        Status = PublicationStatus.Completed;
        ClosedAt = now;
        UpdatedAt = now;
    }

    /// <summary>RN09: encerramento só é permitido sem contratação ativa (publicação aberta).</summary>
    public void Cancel(DateTimeOffset now)
    {
        if (Status != PublicationStatus.Open)
        {
            throw DomainException.Conflict("publication.not_cancellable", "Encerre a contratação ativa antes de encerrar a publicação.");
        }

        Status = PublicationStatus.Cancelled;
        ClosedAt = now;
        UpdatedAt = now;
    }

    /// <summary>RF17: ocultação por moderação. Nunca automática (RN10).</summary>
    public void Hide(string note, DateTimeOffset now)
    {
        if (Status is PublicationStatus.Completed or PublicationStatus.Cancelled or PublicationStatus.Hidden)
        {
            throw DomainException.Conflict("publication.not_hideable", $"Publicações com status '{Status}' não podem ser ocultadas.");
        }

        Status = PublicationStatus.Hidden;
        ModerationNote = Guard.Required(note, "Nota de moderação", 5, 500, "publication.moderation_note");
        UpdatedAt = now;
    }

    /// <summary>Volta a publicação para aberta quando a contratação em negociação é cancelada.</summary>
    public void Reopen(DateTimeOffset now)
    {
        if (Status != PublicationStatus.InNegotiation)
        {
            throw DomainException.Conflict("publication.invalid_transition", $"Não é possível reabrir a partir de '{Status}'.");
        }

        Status = PublicationStatus.Open;
        UpdatedAt = now;
    }

    public void Touch(DateTimeOffset now) => UpdatedAt = now;

    private static PublicationInput Normalize(PublicationInput input, DateTimeOffset now)
    {
        var title = Guard.Required(input.Title, "Título", 5, 150, "publication.title");
        var description = Guard.Required(input.Description, "Descrição", 20, 4000, "publication.description");
        var city = Guard.Required(input.City, "Cidade", 2, 120, "publication.city");
        var region = Guard.Required(input.ServiceRegion, "Região de atendimento", 2, 120, "publication.service_region");
        var neighborhood = Guard.Optional(input.Neighborhood, "Bairro", 120, "publication.neighborhood");

        var budgetMin = Guard.NonNegative(input.BudgetMin, "Orçamento mínimo", "publication.budget_min");
        var budgetMax = Guard.NonNegative(input.BudgetMax, "Orçamento máximo", "publication.budget_max");
        var offeredPrice = Guard.NonNegative(input.OfferedPrice, "Preço ofertado", "publication.offered_price");

        if (budgetMin is not null && budgetMax is not null && budgetMin > budgetMax)
        {
            throw DomainException.Validation("publication.budget_range", "Orçamento mínimo não pode ser maior que o máximo.");
        }

        if (input.Type == PublicationType.Request)
        {
            if (input.Deadline is null)
            {
                throw DomainException.Validation("publication.deadline_required", "Solicitações de demanda exigem prazo.");
            }

            if (input.Deadline <= now)
            {
                throw DomainException.Validation("publication.deadline_past", "O prazo deve ser futuro.");
            }

            if (offeredPrice is not null)
            {
                throw DomainException.Validation("publication.offered_price_not_allowed", "Solicitações usam orçamento, não preço ofertado.");
            }
        }
        else
        {
            if (budgetMin is not null || budgetMax is not null)
            {
                throw DomainException.Validation("publication.budget_not_allowed", "Ofertas de serviço usam preço ofertado, não orçamento.");
            }

            if (input.Deadline is not null && input.Deadline <= now)
            {
                throw DomainException.Validation("publication.deadline_past", "O prazo deve ser futuro.");
            }
        }

        return input with
        {
            CategoryId = Guard.RequiredId(input.CategoryId, "Categoria", "publication.category_id"),
            Title = title,
            Description = description,
            City = city,
            Neighborhood = neighborhood,
            ServiceRegion = region,
            BudgetMin = budgetMin,
            BudgetMax = budgetMax,
            OfferedPrice = offeredPrice
        };
    }
}

public sealed class PublicationStatusHistory
{
    public long Id { get; private set; }
    public Guid PublicationId { get; private set; }
    public Guid? ChangedBy { get; private set; }
    public PublicationStatus OldStatus { get; private set; }
    public PublicationStatus NewStatus { get; private set; }
    public string? Note { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private PublicationStatusHistory()
    {
    }

    public static PublicationStatusHistory Create(
        Guid publicationId,
        Guid? changedBy,
        PublicationStatus oldStatus,
        PublicationStatus newStatus,
        string? note,
        DateTimeOffset now) => new()
    {
        PublicationId = publicationId,
        ChangedBy = changedBy,
        OldStatus = oldStatus,
        NewStatus = newStatus,
        Note = Guard.Optional(note, "Observação", 500, "publication_history.note"),
        CreatedAt = now
    };
}
