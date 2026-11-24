using Tasko.Application.Abstractions;
using Tasko.Application.Contracts;
using Tasko.Domain;
using Tasko.Domain.Repositories;

namespace Tasko.Application.Services;

public sealed class NotificationService(
    INotificationRepository notifications,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IClock clock)
{
    public async Task<PagedResponse<NotificationResponse>> ListAsync(
        bool unreadOnly,
        int page,
        int pageSize,
        CancellationToken ct)
    {
        var userId = currentUser.RequireUserId();
        var (normalizedPage, normalizedSize) = Pagination.Normalize(page, pageSize);
        var result = await notifications.ListByUserAsync(userId, unreadOnly, normalizedPage, normalizedSize, ct);

        return new PagedResponse<NotificationResponse>(
            result.Items.Select(n => n.ToResponse()).ToList(),
            normalizedPage,
            normalizedSize,
            result.Total);
    }

    public async Task<UnreadCountResponse> CountUnreadAsync(CancellationToken ct)
    {
        var userId = currentUser.RequireUserId();
        return new UnreadCountResponse(await notifications.CountUnreadAsync(userId, ct));
    }

    public async Task<NotificationResponse> MarkAsReadAsync(Guid id, CancellationToken ct)
    {
        var userId = currentUser.RequireUserId();
        var notification = await notifications.GetByIdAsync(id, ct)
            ?? throw DomainException.NotFound("notification.not_found", "Notificação não encontrada.");

        if (notification.UserId != userId)
        {
            throw DomainException.Forbidden("notification.forbidden", "Notificação de outro usuário.");
        }

        notification.MarkAsRead(clock.UtcNow);
        await unitOfWork.SaveChangesAsync(ct);
        return notification.ToResponse();
    }

    public async Task<MessageResponse> MarkAllAsReadAsync(CancellationToken ct)
    {
        var userId = currentUser.RequireUserId();
        var now = clock.UtcNow;

        foreach (var notification in await notifications.ListUnreadByUserAsync(userId, ct))
        {
            notification.MarkAsRead(now);
        }

        await unitOfWork.SaveChangesAsync(ct);
        return new MessageResponse("Notificações marcadas como lidas.");
    }
}
