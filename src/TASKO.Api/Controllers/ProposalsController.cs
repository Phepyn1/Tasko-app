using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tasko.Application.Contracts;
using Tasko.Application.Services;

namespace Tasko.Api.Controllers;

[ApiController]
[Route("api/v1/proposals")]
[Authorize]
public sealed class ProposalsController(ProposalService proposals) : ControllerBase
{
    /// <summary>RF20: propostas enviadas pelo usuário autenticado.</summary>
    [HttpGet("mine")]
    public Task<PagedResponse<ProposalResponse>> Mine(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default) =>
        proposals.ListMineAsync(page, pageSize, ct);

    /// <summary>RN04/RN05: aceite atômico, exclusivo do autor da publicação.</summary>
    [HttpPost("{id:guid}/accept")]
    public Task<EngagementResponse> Accept(Guid id, CancellationToken ct) =>
        proposals.AcceptAsync(id, ct);

    [HttpPost("{id:guid}/reject")]
    public Task<ProposalResponse> Reject(Guid id, CancellationToken ct) =>
        proposals.RejectAsync(id, ct);

    [HttpDelete("{id:guid}")]
    public Task<ProposalResponse> Withdraw(Guid id, CancellationToken ct) =>
        proposals.WithdrawAsync(id, ct);
}
