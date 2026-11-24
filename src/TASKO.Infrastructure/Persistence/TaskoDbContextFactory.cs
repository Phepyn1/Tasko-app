using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Tasko.Infrastructure.Persistence;

/// <summary>
/// Usada apenas pelo `dotnet ef` para gerar migrations sem subir a API.
/// </summary>
public sealed class TaskoDbContextFactory : IDesignTimeDbContextFactory<TaskoDbContext>
{
    public TaskoDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("TASKO_DB_CONNECTION")
            ?? "Host=localhost;Port=5432;Database=tasko;Username=tasko;Password=tasko";

        var options = new DbContextOptionsBuilder<TaskoDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new TaskoDbContext(options);
    }
}
