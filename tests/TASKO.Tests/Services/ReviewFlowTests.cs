using Tasko.Application.Contracts;
using Tasko.Domain;

namespace Tasko.Tests.Services;

public sealed class ReviewFlowTests
{
    [Fact]
    public async Task Review_should_fail_before_completion()
    {
        await using var h = new TestHarness();
        var author = await h.CreateUserAsync("Autor", "autor@teste.com", RoleType.Requester);
        var provider = await h.CreateUserAsync("Prestador", "p1@teste.com", RoleType.Provider);
        var publication = await h.CreatePublicationAsync(author, PublicationType.Request);
        var proposal = await h.CreateProposalAsync(publication, provider);
        await h.AcceptAsync(proposal, author);

        h.ActAs(provider);
        var exception = await Assert.ThrowsAsync<DomainException>(
            () => h.Reviews.CreateAsync(
                publication.Id,
                new CreateReviewRequest(author.Id, 5, "Excelente"),
                CancellationToken.None));

        Assert.Equal("review.publication_not_completed", exception.Code);
    }

    [Fact]
    public async Task Both_participants_can_review_after_completion_but_only_once()
    {
        await using var h = new TestHarness();
        var author = await h.CreateUserAsync("Autor", "autor@teste.com", RoleType.Requester);
        var provider = await h.CreateUserAsync("Prestador", "p1@teste.com", RoleType.Provider);
        var publication = await h.CreatePublicationAsync(author, PublicationType.Request);
        var proposal = await h.CreateProposalAsync(publication, provider);

        await h.CompleteAsync(proposal, author);

        h.ActAs(provider);
        var providerReview = await h.Reviews.CreateAsync(
            publication.Id,
            new CreateReviewRequest(author.Id, 5, "Excelente"),
            CancellationToken.None);
        Assert.Equal(5, providerReview.Rating);

        var duplicate = await Assert.ThrowsAsync<DomainException>(
            () => h.Reviews.CreateAsync(
                publication.Id,
                new CreateReviewRequest(author.Id, 4, "De novo"),
                CancellationToken.None));
        Assert.Equal("review.duplicate", duplicate.Code);

        h.ActAs(author);
        var authorReview = await h.Reviews.CreateAsync(
            publication.Id,
            new CreateReviewRequest(provider.Id, 4, "Bom"),
            CancellationToken.None);
        Assert.Equal(4, authorReview.Rating);

        var (average, count) = await h.ReviewsRepository.GetRatingAsync(provider.Id, CancellationToken.None);
        Assert.Equal(4, average);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task Review_should_block_self_review_via_service()
    {
        await using var h = new TestHarness();
        var author = await h.CreateUserAsync("Autor", "autor@teste.com", RoleType.Requester);
        var provider = await h.CreateUserAsync("Prestador", "p1@teste.com", RoleType.Provider);
        var publication = await h.CreatePublicationAsync(author, PublicationType.Request);
        var proposal = await h.CreateProposalAsync(publication, provider);

        await h.CompleteAsync(proposal, author);

        h.ActAs(provider);
        var exception = await Assert.ThrowsAsync<DomainException>(
            () => h.Reviews.CreateAsync(
                publication.Id,
                new CreateReviewRequest(provider.Id, 5, null),
                CancellationToken.None));

        Assert.Equal("review.self", exception.Code);
    }

    [Fact]
    public async Task Review_should_block_non_participant_via_service()
    {
        await using var h = new TestHarness();
        var author = await h.CreateUserAsync("Autor", "autor@teste.com", RoleType.Requester);
        var provider = await h.CreateUserAsync("Prestador", "p1@teste.com", RoleType.Provider);
        var outsider = await h.CreateUserAsync("Intruso", "intruso@teste.com", RoleType.Provider);
        var publication = await h.CreatePublicationAsync(author, PublicationType.Request);
        var proposal = await h.CreateProposalAsync(publication, provider);

        await h.CompleteAsync(proposal, author);

        h.ActAs(outsider);
        var exception = await Assert.ThrowsAsync<DomainException>(
            () => h.Reviews.CreateAsync(
                publication.Id,
                new CreateReviewRequest(author.Id, 1, "Não participei"),
                CancellationToken.None));

        Assert.Equal("review.not_participant", exception.Code);
    }
}
