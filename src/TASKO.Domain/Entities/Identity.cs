namespace Tasko.Domain.Entities;

public sealed class User
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Name { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public bool IsActive { get; private set; } = true;
    public DateTimeOffset? EmailVerifiedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }

    public List<UserRole> Roles { get; private set; } = [];
    public UserProfile? Profile { get; private set; }
    public List<UserSkill> Skills { get; private set; } = [];

    private User()
    {
    }

    public static User Create(string name, string email, string passwordHash, DateTimeOffset now)
    {
        var normalizedEmail = NormalizeEmail(email);

        return new User
        {
            Name = Guard.Required(name, "Nome", 2, 120, "user.name"),
            Email = normalizedEmail,
            PasswordHash = string.IsNullOrWhiteSpace(passwordHash)
                ? throw DomainException.Validation("user.password_hash", "Hash de senha é obrigatório.")
                : passwordHash,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public static string NormalizeEmail(string? email)
    {
        var normalized = (email ?? string.Empty).Trim().ToLowerInvariant();
        if (normalized.Length < 5 || normalized.Length > 180 || !normalized.Contains('@') || normalized.StartsWith('@') || normalized.EndsWith('@'))
        {
            throw DomainException.Validation("user.email", "E-mail inválido.");
        }

        return normalized;
    }

    public void UpdatePassword(string passwordHash, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            throw DomainException.Validation("user.password_hash", "Hash de senha é obrigatório.");
        }

        PasswordHash = passwordHash;
        UpdatedAt = now;
    }

    /// <summary>RF03/RN01: papéis de negócio são autoatribuídos; moderador nunca é.</summary>
    public void AddRole(RoleType role, DateTimeOffset now)
    {
        if (role == RoleType.Moderator)
        {
            throw DomainException.Forbidden("user.role.moderator", "O papel de moderador não pode ser autoatribuído.");
        }

        if (Roles.Any(r => r.Role == role))
        {
            return;
        }

        Roles.Add(UserRole.Create(Id, role, now));
        UpdatedAt = now;
    }

    /// <summary>Uso restrito ao bootstrap administrativo (seed), nunca a um endpoint público.</summary>
    public void GrantModerator(DateTimeOffset now)
    {
        if (Roles.Any(r => r.Role == RoleType.Moderator))
        {
            return;
        }

        Roles.Add(UserRole.Create(Id, RoleType.Moderator, now));
        UpdatedAt = now;
    }

    public bool HasRole(RoleType role) => Roles.Any(r => r.Role == role);

    public UserProfile EnsureProfile(DateTimeOffset now) => Profile ??= UserProfile.CreateEmpty(Id, now);

    public void ReplaceSkills(IEnumerable<(Guid CategoryId, string Name)> skills, DateTimeOffset now)
    {
        Skills.Clear();
        foreach (var (categoryId, name) in skills)
        {
            Skills.Add(UserSkill.Create(Id, categoryId, name, now));
        }

        UpdatedAt = now;
    }

    /// <summary>Marca a atualização das habilidades quando a persistência é feita pelo repositório.</summary>
    public void MarkSkillsUpdated(DateTimeOffset now) => UpdatedAt = now;

    public void EnsureActive()
    {
        if (!IsActive || DeletedAt is not null)
        {
            throw DomainException.Forbidden("user.inactive", "Conta inativa.");
        }
    }

    public void MarkEmailVerified(DateTimeOffset now)
    {
        EmailVerifiedAt ??= now;
        UpdatedAt = now;
    }

    public void SoftDelete(DateTimeOffset now)
    {
        IsActive = false;
        DeletedAt = now;
        UpdatedAt = now;
    }
}

public sealed class UserRole
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid UserId { get; private set; }
    public RoleType Role { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private UserRole()
    {
    }

    public static UserRole Create(Guid userId, RoleType role, DateTimeOffset now) => new()
    {
        UserId = Guard.RequiredId(userId, "Usuário", "user_role.user_id"),
        Role = role,
        CreatedAt = now
    };
}

public sealed class UserProfile
{
    public Guid UserId { get; private set; }
    public string? Presentation { get; private set; }
    public string? Phone { get; private set; }
    public string? City { get; private set; }
    public string? Description { get; private set; }
    public string? Neighborhood { get; private set; }
    public string? ServiceRegion { get; private set; }
    public string? ProfilePhotoUrl { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private UserProfile()
    {
    }

    public static UserProfile CreateEmpty(Guid userId, DateTimeOffset now) => new()
    {
        UserId = Guard.RequiredId(userId, "Usuário", "user_profile.user_id"),
        UpdatedAt = now
    };

    public void Update(
        string? presentation,
        string? phone,
        string? city,
        string? description,
        string? neighborhood,
        string? serviceRegion,
        string? profilePhotoUrl,
        DateTimeOffset now)
    {
        Presentation = Guard.Optional(presentation, "Apresentação", 500, "user_profile.presentation");
        Phone = Guard.Optional(phone, "Telefone", 20, "user_profile.phone");
        City = Guard.Optional(city, "Cidade", 120, "user_profile.city");
        Description = Guard.Optional(description, "Descrição", 2000, "user_profile.description");
        Neighborhood = Guard.Optional(neighborhood, "Bairro", 120, "user_profile.neighborhood");
        ServiceRegion = Guard.Optional(serviceRegion, "Região de atendimento", 200, "user_profile.service_region");
        ProfilePhotoUrl = Guard.Optional(profilePhotoUrl, "Foto de perfil", 500, "user_profile.photo_url");

        if (Phone is not null && Phone.Count(char.IsDigit) < 10)
        {
            throw DomainException.Validation("user_profile.phone", "Telefone deve conter ao menos 10 dígitos.");
        }

        UpdatedAt = now;
    }
}

public sealed class UserSkill
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid UserId { get; private set; }
    public Guid CategoryId { get; private set; }
    public Category? Category { get; private set; }
    public string SkillName { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }

    private UserSkill()
    {
    }

    public static UserSkill Create(Guid userId, Guid categoryId, string skillName, DateTimeOffset now) => new()
    {
        UserId = Guard.RequiredId(userId, "Usuário", "user_skill.user_id"),
        CategoryId = Guard.RequiredId(categoryId, "Categoria", "user_skill.category_id"),
        SkillName = Guard.Required(skillName, "Habilidade", 2, 80, "user_skill.skill_name"),
        CreatedAt = now
    };
}

public sealed class PasswordResetToken
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid UserId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? UsedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private PasswordResetToken()
    {
    }

    public static PasswordResetToken Create(Guid userId, string tokenHash, DateTimeOffset expiresAt, DateTimeOffset now)
    {
        if (expiresAt <= now)
        {
            throw DomainException.Validation("password_reset.expires_at", "Expiração do token deve ser futura.");
        }

        return new PasswordResetToken
        {
            UserId = Guard.RequiredId(userId, "Usuário", "password_reset.user_id"),
            TokenHash = Guard.Required(tokenHash, "Hash do token", 32, 128, "password_reset.token_hash"),
            ExpiresAt = expiresAt,
            CreatedAt = now
        };
    }

    public bool IsUsable(DateTimeOffset now) => UsedAt is null && ExpiresAt > now;

    public void Consume(DateTimeOffset now)
    {
        if (!IsUsable(now))
        {
            throw DomainException.Conflict("password_reset.unusable", "Token de recuperação inválido ou expirado.");
        }

        UsedAt = now;
    }
}

public sealed class RefreshToken
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid UserId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public string? ReplacedByTokenHash { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public string? IpAddress { get; private set; }

    private RefreshToken()
    {
    }

    public static RefreshToken Create(Guid userId, string tokenHash, DateTimeOffset expiresAt, DateTimeOffset now, string? ipAddress)
    {
        if (expiresAt <= now)
        {
            throw DomainException.Validation("refresh_token.expires_at", "Expiração do refresh token deve ser futura.");
        }

        return new RefreshToken
        {
            UserId = Guard.RequiredId(userId, "Usuário", "refresh_token.user_id"),
            TokenHash = Guard.Required(tokenHash, "Hash do token", 32, 128, "refresh_token.token_hash"),
            ExpiresAt = expiresAt,
            CreatedAt = now,
            IpAddress = Guard.Optional(ipAddress, "IP", 45, "refresh_token.ip")
        };
    }

    public bool IsActive(DateTimeOffset now) => RevokedAt is null && ExpiresAt > now;

    public void Revoke(DateTimeOffset now, string? replacedByTokenHash = null)
    {
        RevokedAt ??= now;
        ReplacedByTokenHash ??= replacedByTokenHash;
    }
}
