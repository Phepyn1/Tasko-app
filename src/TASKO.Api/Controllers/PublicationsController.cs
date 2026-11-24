using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tasko.Application.Contracts;
using Tasko.Application.Services;
using Tasko.Domain;

namespace Tasko.Api.Controllers;

[ApiController]
[Route("api/v1/publications")]
public sealed class PublicationsController(
    PublicationService publications,
    ProposalService proposals,
    ReviewService reviews) : ControllerBase
{
    /// <summary>RF07/RF08: listagem pesquisável com filtros e paginação obrigatória.</summary>
    [HttpGet]
    [AllowAnonymous]
    public Task<PagedResponse<PublicationResponse>> Search(
        [FromQuery] PublicationType? type,
        [FromQuery] Guid? categoryId,
        [FromQuery] string? city,
        [FromQuery] string? serviceRegion,
        [FromQuery] string? term,
        [FromQuery] PublicationStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default) =>
        publications.SearchAsync(type, categoryId, city, serviceRegion, term, status, page, pageSize, ct);

    [HttpPost]
    [Authorize]
    [ProducesResponseType(typeof(PublicationResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<PublicationResponse>> Create(
        CreatePublicationRequest request,
        CancellationToken ct)
    {
        var created = await publications.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>RF20: histórico de publicações do usuário.</summary>
    [HttpGet("mine")]
    [Authorize]
    public Task<PagedResponse<PublicationResponse>> Mine(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default) =>
        publications.ListMineAsync(page, pageSize, ct);

    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public Task<PublicationDetailResponse> GetById(Guid id, CancellationToken ct) =>
        publications.GetAsync(id, ct);

    [HttpPut("{id:guid}")]
    [Authorize]
    public Task<PublicationResponse> Update(
        Guid id,
        UpdatePublicationRequest request,
        CancellationToken ct) =>
        publications.UpdateAsync(id, request, ct);

    /// <summary>RN09: encerramento só sem contratação ativa.</summary>
    [HttpPost("{id:guid}/cancel")]
    [Authorize]
    public Task<PublicationResponse> Cancel(Guid id, CancellationToken ct) =>
        publications.CancelAsync(id, ct);

    [HttpGet("{id:guid}/history")]
    [AllowAnonymous]
    public async Task<IReadOnlyList<StatusHistoryResponse>> History(Guid id, CancellationToken ct)
    {
        var detail = await publications.GetAsync(id, ct);
        return detail.StatusHistory;
    }

    [HttpPost("{id:guid}/proposals")]
    [Authorize]
    [ProducesResponseType(typeof(ProposalResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<ProposalResponse>> CreateProposal(
        Guid id,
        CreateProposalRequest request,
        CancellationToken ct)
    {
        var proposal = await proposals.CreateAsync(id, request, ct);
        return StatusCode(StatusCodes.Status201Created, proposal);
    }

    /// <summary>RF11: propostas recebidas, com dados públicos do proponente.</summary>
    [HttpGet("{id:guid}/proposals")]
    [Authorize]
    public Task<IReadOnlyList<ProposalResponse>> ListProposals(Guid id, CancellationToken ct) =>
        proposals.ListForPublicationAsync(id, ct);

    [HttpPost("{id:guid}/reviews")]
    [Authorize]
    [ProducesResponseType(typeof(ReviewResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<ReviewResponse>> CreateReview(
        Guid id,
        CreateReviewRequest request,
        CancellationToken ct)
    {
        var review = await reviews.CreateAsync(id, request, ct);
        return StatusCode(StatusCodes.Status201Created, review);
    }

    [HttpGet("{id:guid}/reviews")]
    [AllowAnonymous]
    public Task<PagedResponse<ReviewResponse>> ListReviews(
        Guid id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default) =>
        reviews.ListByPublicationAsync(id, page, pageSize, ct);
}
