using System.Text.Json;
using Tasko.Application.Abstractions;
using Tasko.Domain.Entities;
using Tasko.Domain.Repositories;

namespace Tasko.Application.Services;

/// <summary>RNF11: toda ação crítica chama este serviço antes do SaveChanges da operação.</summary>
public sealed class AuditService(
    IActivityLogRepository logs,
    ICurrentUser currentUser,
    IClock clock)
{
    public void Record(string action, string entityType, object? entityId, object? metadata = null)
    {
        var metadataJson = metadata is null ? null : JsonSerializer.Serialize(metadata);
        logs.Add(ActivityLog.Create(
            currentUser.UserId,
            action,
            entityType,
            entityId?.ToString(),
            metadataJson,
            currentUser.IpAddress,
            clock.UtcNow));
    }
}
