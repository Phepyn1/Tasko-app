using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tasko.Application.Abstractions;
using Tasko.Domain.Repositories;
using Tasko.Infrastructure.Email;
using Tasko.Infrastructure.Persistence;
using Tasko.Infrastructure.Security;

namespace Tasko.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registra a persistência. <paramref name="provider"/> = "InMemory" habilita o modo demonstração
    /// (sem PostgreSQL); qualquer outro valor usa Npgsql, o padrão de produção.
    /// </summary>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        string connectionString,
        string? provider = null,
        string? inMemoryDatabaseName = null)
    {
        var useInMemory = string.Equals(provider, "InMemory", StringComparison.OrdinalIgnoreCase);

        services.AddDbContext<TaskoDbContext>(options =>
        {
            if (useInMemory)
            {
                options.UseInMemoryDatabase(inMemoryDatabaseName ?? "tasko-demo");
                return;
            }

            // Sem retry automático: o fluxo de aceite usa transação explícita.
            options.UseNpgsql(connectionString);
        });

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<IPublicationRepository, PublicationRepository>();
        services.AddScoped<IProposalRepository, ProposalRepository>();
        services.AddScoped<IEngagementRepository, EngagementRepository>();
        services.AddScoped<IReviewRepository, ReviewRepository>();
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<IReportRepository, ReportRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IPasswordResetTokenRepository, PasswordResetTokenRepository>();
        services.AddScoped<IActivityLogRepository, ActivityLogRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();
        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddSingleton<IEmailSender, LogEmailSender>();

        return services;
    }
}
