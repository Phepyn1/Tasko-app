using Microsoft.EntityFrameworkCore;
using Tasko.Application.Abstractions;
using Tasko.Domain.Entities;
using Tasko.Infrastructure.Persistence;

namespace Tasko.Api.Seed;

public static class DatabaseSeeder
{
    private static readonly (string Name, string Description)[] DefaultCategories =
    [
        ("Construção e Reformas", "Alvenaria, gesso, telhado e pequenas reformas."),
        ("Elétrica", "Instalações e reparos elétricos."),
        ("Hidráulica", "Encanamento, vazamentos e instalações."),
        ("Limpeza", "Limpeza residencial e comercial."),
        ("Jardinagem", "Poda, plantio e manutenção de jardins."),
        ("Pintura", "Pintura predial e residencial."),
        ("Montagem de Móveis", "Montagem e desmontagem de móveis."),
        ("Tecnologia", "Suporte técnico, redes e desenvolvimento."),
        ("Aulas Particulares", "Reforço escolar, idiomas e música."),
        ("Eventos", "Fotografia, buffet e organização de eventos."),
        ("Transporte e Mudanças", "Fretes, mudanças e transporte de itens."),
        ("Beleza e Bem-estar", "Cabelo, estética e cuidados pessoais.")
    ];

    public static async Task SeedAsync(IServiceProvider services, IConfiguration configuration)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TaskoDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Seed");
        var now = DateTimeOffset.UtcNow;

        if (!await db.Categories.AnyAsync())
        {
            await db.Categories.AddRangeAsync(
                DefaultCategories.Select(c => Category.Create(c.Name, c.Description, now)));
            await db.SaveChangesAsync();
            logger.LogInformation("Categorias padrão semeadas ({Count}).", DefaultCategories.Length);
        }

        var moderatorEmail = configuration["Seed:ModeratorEmail"];
        var moderatorPassword = configuration["Seed:ModeratorPassword"];
        if (string.IsNullOrWhiteSpace(moderatorEmail) || string.IsNullOrWhiteSpace(moderatorPassword))
        {
            return;
        }

        var email = User.NormalizeEmail(moderatorEmail);
        if (await db.Users.AnyAsync(u => u.Email == email))
        {
            return;
        }

        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var moderator = User.Create("Moderação TASKO", email, hasher.Hash(moderatorPassword), now);
        moderator.GrantModerator(now);
        moderator.EnsureProfile(now);
        moderator.MarkEmailVerified(now);

        db.Users.Add(moderator);
        await db.SaveChangesAsync();
        logger.LogInformation("Usuário moderador semeado: {Email}", email);
    }
}
