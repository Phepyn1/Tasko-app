using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Tasko.Domain;
using Tasko.Domain.Entities;

namespace Tasko.Infrastructure.Persistence;

public sealed class TaskoDbContext(DbContextOptions<TaskoDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
    public DbSet<UserSkill> UserSkills => Set<UserSkill>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Publication> Publications => Set<Publication>();
    public DbSet<PublicationStatusHistory> PublicationStatusHistories => Set<PublicationStatusHistory>();
    public DbSet<Proposal> Proposals => Set<Proposal>();
    public DbSet<Engagement> Engagements => Set<Engagement>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<Report> Reports => Set<Report>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<ActivityLog> ActivityLogs => Set<ActivityLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigureIdentity(modelBuilder);
        ConfigureCatalog(modelBuilder);
        ConfigureNegotiation(modelBuilder);
        ConfigureGovernance(modelBuilder);
        ApplySnakeCaseNames(modelBuilder);
    }

    private static void ConfigureIdentity(ModelBuilder b)
    {
        b.Entity<User>(e =>
        {
            e.ToTable("users");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(120).IsRequired();
            e.Property(x => x.Email).HasMaxLength(180).IsRequired();
            e.Property(x => x.PasswordHash).HasMaxLength(200).IsRequired();
            e.Property(x => x.IsActive).IsRequired();
            e.HasIndex(x => x.Email).IsUnique().HasFilter("deleted_at IS NULL");
            e.HasMany(x => x.Roles).WithOne().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Profile).WithOne().HasForeignKey<UserProfile>(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Skills).WithOne().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<UserRole>(e =>
        {
            e.ToTable("user_roles");
            e.HasKey(x => x.Id);
            e.Property(x => x.Role)
                .HasConversion(RoleConverter)
                .HasMaxLength(20)
                .IsRequired();
            e.HasIndex(x => new { x.UserId, x.Role }).IsUnique();
        });

        b.Entity<UserProfile>(e =>
        {
            e.ToTable("user_profiles");
            e.HasKey(x => x.UserId);
            e.Property(x => x.Presentation).HasMaxLength(500);
            e.Property(x => x.Phone).HasMaxLength(20);
            e.Property(x => x.City).HasMaxLength(120);
            e.Property(x => x.Description).HasMaxLength(2000);
            e.Property(x => x.Neighborhood).HasMaxLength(120);
            e.Property(x => x.ServiceRegion).HasMaxLength(200);
            e.Property(x => x.ProfilePhotoUrl).HasMaxLength(500);
        });

        b.Entity<UserSkill>(e =>
        {
            e.ToTable("user_skills");
            e.HasKey(x => x.Id);
            e.Property(x => x.SkillName).HasMaxLength(80).IsRequired();
            e.HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.UserId, x.SkillName });
        });

        b.Entity<PasswordResetToken>(e =>
        {
            e.ToTable("password_reset_tokens");
            e.HasKey(x => x.Id);
            e.Property(x => x.TokenHash).HasMaxLength(128).IsRequired();
            e.HasIndex(x => x.TokenHash).IsUnique();
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<RefreshToken>(e =>
        {
            e.ToTable("refresh_tokens");
            e.HasKey(x => x.Id);
            e.Property(x => x.TokenHash).HasMaxLength(128).IsRequired();
            e.Property(x => x.ReplacedByTokenHash).HasMaxLength(128);
            e.Property(x => x.IpAddress).HasMaxLength(45);
            e.HasIndex(x => x.TokenHash).IsUnique();
            e.HasIndex(x => x.UserId);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureCatalog(ModelBuilder b)
    {
        b.Entity<Category>(e =>
        {
            e.ToTable("categories");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(80).IsRequired();
            e.Property(x => x.Description).HasMaxLength(500);
            e.HasIndex(x => x.Name).IsUnique();
        });

        b.Entity<Publication>(e =>
        {
            e.ToTable("publications");
            e.HasKey(x => x.Id);
            e.Property(x => x.Type).HasConversion<string>().HasMaxLength(20).IsRequired();
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
            e.Property(x => x.Title).HasMaxLength(150).IsRequired();
            e.Property(x => x.Description).HasMaxLength(4000).IsRequired();
            e.Property(x => x.City).HasMaxLength(120).IsRequired();
            e.Property(x => x.Neighborhood).HasMaxLength(120);
            e.Property(x => x.ServiceRegion).HasMaxLength(120).IsRequired();
            e.Property(x => x.BudgetMin).HasPrecision(12, 2);
            e.Property(x => x.BudgetMax).HasPrecision(12, 2);
            e.Property(x => x.OfferedPrice).HasPrecision(12, 2);
            e.Property(x => x.ModerationNote).HasMaxLength(500);

            e.HasOne(x => x.Author).WithMany().HasForeignKey(x => x.AuthorId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x => x.Proposals).WithOne(x => x.Publication).HasForeignKey(x => x.PublicationId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x => x.StatusHistory).WithOne().HasForeignKey(x => x.PublicationId).OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(x => new { x.Status, x.CategoryId, x.City, x.CreatedAt });
            e.HasIndex(x => new { x.AuthorId, x.CreatedAt });
        });

        b.Entity<PublicationStatusHistory>(e =>
        {
            e.ToTable("publication_status_history");
            e.HasKey(x => x.Id);
            e.Property(x => x.OldStatus).HasConversion<string>().HasMaxLength(30).IsRequired();
            e.Property(x => x.NewStatus).HasConversion<string>().HasMaxLength(30).IsRequired();
            e.Property(x => x.Note).HasMaxLength(500);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.ChangedBy).OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(x => x.PublicationId);
        });
    }

    private static void ConfigureNegotiation(ModelBuilder b)
    {
        b.Entity<Proposal>(e =>
        {
            e.ToTable("proposals");
            e.HasKey(x => x.Id);
            e.Property(x => x.Message).HasMaxLength(2000).IsRequired();
            e.Property(x => x.ProposedPrice).HasPrecision(12, 2);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
            e.HasOne(x => x.Publication).WithMany(x => x.Proposals).HasForeignKey(x => x.PublicationId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Proposer).WithMany().HasForeignKey(x => x.ProposerId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.PublicationId, x.Status });
            e.HasIndex(x => new { x.ProposerId, x.CreatedAt });
        });

        b.Entity<Engagement>(e =>
        {
            e.ToTable("engagements");
            e.HasKey(x => x.Id);
            e.Property(x => x.AgreedPrice).HasPrecision(12, 2);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
            e.HasOne(x => x.Publication).WithMany().HasForeignKey(x => x.PublicationId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Proposer).WithMany().HasForeignKey(x => x.ProposerId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Receiver).WithMany().HasForeignKey(x => x.ReceiverId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Provider).WithMany().HasForeignKey(x => x.ProviderId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.PublicationId).IsUnique().HasFilter("status = 'Active'");
            e.HasIndex(x => new { x.ProposerId, x.CreatedAt });
            e.HasIndex(x => new { x.ReceiverId, x.CreatedAt });
        });

        b.Entity<Review>(e =>
        {
            e.ToTable("reviews");
            e.HasKey(x => x.Id);
            e.Property(x => x.Rating).IsRequired();
            e.Property(x => x.Comment).HasMaxLength(1000);
            e.HasOne(x => x.Publication).WithMany().HasForeignKey(x => x.PublicationId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Engagement).WithMany().HasForeignKey(x => x.EngagementId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Reviewer).WithMany().HasForeignKey(x => x.ReviewerId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Reviewed).WithMany().HasForeignKey(x => x.ReviewedId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.PublicationId, x.ReviewerId }).IsUnique();
            e.HasIndex(x => x.ReviewedId);
            e.ToTable(t => t.HasCheckConstraint("ck_reviews_rating", "rating BETWEEN 1 AND 5"));
        });
    }

    private static void ConfigureGovernance(ModelBuilder b)
    {
        b.Entity<Report>(e =>
        {
            e.ToTable("reports");
            e.HasKey(x => x.Id);
            e.Property(x => x.Reason).HasConversion<string>().HasMaxLength(20).IsRequired();
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
            e.Property(x => x.Description).HasMaxLength(1000);
            e.Property(x => x.ModerationNote).HasMaxLength(500);
            e.HasOne(x => x.Reporter).WithMany().HasForeignKey(x => x.ReporterId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.ReportedUser).WithMany().HasForeignKey(x => x.ReportedUserId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.Publication).WithMany().HasForeignKey(x => x.PublicationId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.Moderator).WithMany().HasForeignKey(x => x.ModeratorId).OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(x => new { x.Status, x.CreatedAt });
            e.ToTable(t => t.HasCheckConstraint(
                "ck_reports_target",
                "reported_user_id IS NOT NULL OR publication_id IS NOT NULL"));
        });

        b.Entity<Notification>(e =>
        {
            e.ToTable("notifications");
            e.HasKey(x => x.Id);
            e.Property(x => x.Type).HasConversion<string>().HasMaxLength(40).IsRequired();
            e.Property(x => x.Title).HasMaxLength(150).IsRequired();
            e.Property(x => x.Message).HasMaxLength(500).IsRequired();
            e.Property(x => x.ReferenceType).HasMaxLength(60);
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.UserId, x.ReadAt, x.CreatedAt });
        });

        b.Entity<ActivityLog>(e =>
        {
            e.ToTable("activity_logs");
            e.HasKey(x => x.Id);
            e.Property(x => x.Action).HasMaxLength(100).IsRequired();
            e.Property(x => x.EntityType).HasMaxLength(60).IsRequired();
            e.Property(x => x.EntityId).HasMaxLength(64);
            e.Property(x => x.Metadata).HasColumnType("jsonb");
            e.Property(x => x.IpAddress).HasMaxLength(45);
            e.HasOne(x => x.Actor).WithMany().HasForeignKey(x => x.ActorId).OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(x => x.CreatedAt);
            e.HasIndex(x => new { x.EntityType, x.EntityId });
        });
    }

    private static readonly ValueConverter<RoleType, string> RoleConverter = new(
        role => role == RoleType.Requester ? "solicitante" : role == RoleType.Provider ? "prestador" : "moderador",
        value => value == "solicitante" ? RoleType.Requester : value == "prestador" ? RoleType.Provider : RoleType.Moderator);

    /// <summary>Converte tabelas/colunas/índices/constraints para snake_case (aderência ao DER).</summary>
    private static void ApplySnakeCaseNames(ModelBuilder modelBuilder)
    {
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            if (entity.GetTableName() is { } tableName)
            {
                entity.SetTableName(ToSnakeCase(tableName));
            }

            foreach (var property in entity.GetProperties())
            {
                property.SetColumnName(ToSnakeCase(property.Name));
            }

            foreach (var key in entity.GetKeys())
            {
                key.SetName(ToSnakeCase(key.GetName()!));
            }

            foreach (var foreignKey in entity.GetForeignKeys())
            {
                foreignKey.SetConstraintName(ToSnakeCase(foreignKey.GetConstraintName()!));
            }

            foreach (var index in entity.GetIndexes())
            {
                index.SetDatabaseName(ToSnakeCase(index.GetDatabaseName()!));
            }
        }
    }

    private static string ToSnakeCase(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return name;
        }

        var builder = new StringBuilder(name.Length + 8);
        for (var i = 0; i < name.Length; i++)
        {
            var current = name[i];
            if (!char.IsUpper(current))
            {
                builder.Append(current);
                continue;
            }

            var previousIsLowerOrDigit = i > 0 && (char.IsLower(name[i - 1]) || char.IsDigit(name[i - 1]));
            var nextIsLower = i + 1 < name.Length && char.IsLower(name[i + 1]);
            if (previousIsLowerOrDigit || (nextIsLower && i > 0 && char.IsUpper(name[i - 1])))
            {
                builder.Append('_');
            }

            builder.Append(char.ToLowerInvariant(current));
        }

        return builder.ToString();
    }
}
