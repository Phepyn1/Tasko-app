using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tasko.Application.Contracts;
using Tasko.Application.Services;

namespace Tasko.Api.Controllers;

[ApiController]
[Route("api/v1/engagements")]
[Authorize]
public sealed class EngagementsController(EngagementService engagements) : ControllerBase
{
    [HttpGet]
    public Task<PagedResponse<EngagementResponse>> Mine(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default) =>
        engagements.ListMineAsync(page, pageSize, ct);

    [HttpGet("{id:guid}")]
    public Task<EngagementResponse> GetById(Guid id, CancellationToken ct) =>
        engagements.GetAsync(id, ct);

    /// <summary>RF13/RN06: contato direto apenas para os dois participantes após o aceite.</summary>
    [HttpGet("{id:guid}/contact")]
    public Task<ContactResponse> Contact(Guid id, CancellationToken ct) =>
        engagements.GetContactAsync(id, ct);

    /// <summary>RN05: InNegotiation → Agreed.</summary>
    [HttpPost("{id:guid}/confirm")]
    public Task<EngagementResponse> Confirm(Guid id, CancellationToken ct) =>
        engagements.ConfirmAsync(id, ct);

    /// <summary>RN07: conclusão libera as avaliações.</summary>
    [HttpPost("{id:guid}/complete")]
    public Task<EngagementResponse> Complete(Guid id, CancellationToken ct) =>
        engagements.CompleteAsync(id, ct);

    [HttpPost("{id:guid}/cancel")]
    public Task<EngagementResponse> Cancel(Guid id, CancellationToken ct) =>
        engagements.CancelAsync(id, ct);
}
