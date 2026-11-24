using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tasko.Application.Contracts;
using Tasko.Application.Services;

namespace Tasko.Api.Controllers;

[ApiController]
[Route("api/v1/notifications")]
[Authorize]
public sealed class NotificationsController(NotificationService notifications) : ControllerBase
{
    [HttpGet]
    public Task<PagedResponse<NotificationResponse>> List(
        [FromQuery] bool unreadOnly = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default) =>
        notifications.ListAsync(unreadOnly, page, pageSize, ct);

    [HttpGet("unread-count")]
    public Task<UnreadCountResponse> UnreadCount(CancellationToken ct) =>
        notifications.CountUnreadAsync(ct);

    [HttpPost("{id:guid}/read")]
    public Task<NotificationResponse> MarkAsRead(Guid id, CancellationToken ct) =>
        notifications.MarkAsReadAsync(id, ct);

    [HttpPost("read-all")]
    public Task<MessageResponse> MarkAllAsRead(CancellationToken ct) =>
        notifications.MarkAllAsReadAsync(ct);
}
