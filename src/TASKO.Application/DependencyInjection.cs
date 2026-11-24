using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Tasko.Application.Services;
using Tasko.Application.Validators;

namespace Tasko.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<AuditService>();
        services.AddScoped<AuthService>();
        services.AddScoped<ProfileService>();
        services.AddScoped<CategoryService>();
        services.AddScoped<PublicationService>();
        services.AddScoped<ProposalService>();
        services.AddScoped<EngagementService>();
        services.AddScoped<ReviewService>();
        services.AddScoped<NotificationService>();
        services.AddScoped<ReportService>();

        services.AddValidatorsFromAssemblyContaining<RegisterRequestValidator>();
        return services;
    }
}
