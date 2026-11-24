using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tasko.Application.Contracts;
using Tasko.Application.Services;
using Tasko.Domain;

namespace Tasko.Api.Controllers;

[ApiController]
[Route("api/v1/reports")]
[Authorize]
public sealed class ReportsController(ReportService reports) : ControllerBase
{
    /// <summary>RF16/RN10: apenas enfileira para moderação.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ReportResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<ReportResponse>> Create(
        CreateReportRequest request,
        CancellationToken ct)
    {
        var report = await reports.CreateAsync(request, ct);
        return StatusCode(StatusCodes.Status201Created, report);
    }
}

[ApiController]
[Route("api/v1/admin/reports")]
[Authorize(Policy = "Moderator")]
public sealed class AdminReportsController(ReportService reports) : ControllerBase
{
    /// <summary>RF17: fila de moderação.</summary>
    [HttpGet]
    public Task<PagedResponse<ReportResponse>> List(
        [FromQuery] ReportStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default) =>
        reports.ListAsync(status, page, pageSize, ct);

    /// <summary>RF17: decisão de moderação (dismiss ou hide).</summary>
    [HttpPost("{id:guid}/resolve")]
    public Task<ReportResponse> Resolve(
        Guid id,
        ResolveReportRequest request,
        CancellationToken ct) =>
        reports.ResolveAsync(id, request, ct);
}
