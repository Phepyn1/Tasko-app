using Tasko.Domain;
using Tasko.Domain.Entities;

namespace Tasko.Tests.Domain;

public sealed class PublicationDomainTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 10, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid CategoryId = Guid.NewGuid();

    private static PublicationInput RequestInput(
        DateTimeOffset? deadline = null,
        decimal? budgetMin = 100m,
        decimal? budgetMax = 300m,
        decimal? offeredPrice = null) =>
        new(
            PublicationType.Request,
            CategoryId,
            "Preciso de eletricista",
            "Troca de disjuntor e revisão da fiação antiga do imóvel.",
            "São Paulo",
            "Centro",
            "Zona Central",
            budgetMin,
            budgetMax,
            offeredPrice,
            deadline ?? Now.AddDays(3));

    private static PublicationInput OfferInput(decimal? offeredPrice = 250m, decimal? budgetMin = null, decimal? budgetMax = null) =>
        new(
            PublicationType.Offer,
            CategoryId,
            "Faço instalações elétricas",
            "Atendo residências e comércios com instalação e manutenção elétrica.",
            "São Paulo",
            "Centro",
            "Zona Central",
            budgetMin,
            budgetMax,
            offeredPrice,
            null);

    [Fact]
    public void Request_without_deadline_should_fail()
    {
        var input = RequestInput() with { Deadline = null };

        var exception = Assert.Throws<DomainException>(() => Publication.Create(Guid.NewGuid(), input, Now));

        Assert.Equal("publication.deadline_required", exception.Code);
    }

    [Fact]
    public void Request_with_offered_price_should_fail()
    {
        var exception = Assert.Throws<DomainException>(
            () => Publication.Create(Guid.NewGuid(), RequestInput(offeredPrice: 50m), Now));

        Assert.Equal("publication.offered_price_not_allowed", exception.Code);
    }

    [Fact]
    public void Offer_with_budget_should_fail()
    {
        var exception = Assert.Throws<DomainException>(
            () => Publication.Create(Guid.NewGuid(), OfferInput(budgetMin: 10m, budgetMax: 20m), Now));

        Assert.Equal("publication.budget_not_allowed", exception.Code);
    }

    [Fact]
    public void Budget_min_greater_than_max_should_fail()
    {
        var exception = Assert.Throws<DomainException>(
            () => Publication.Create(Guid.NewGuid(), RequestInput(budgetMin: 500m, budgetMax: 100m), Now));

        Assert.Equal("publication.budget_range", exception.Code);
    }

    [Fact]
    public void Update_should_be_blocked_when_there_is_active_engagement()
    {
        var publication = Publication.Create(Guid.NewGuid(), RequestInput(), Now);

        var exception = Assert.Throws<DomainException>(
            () => publication.Update(RequestInput(), hasActiveEngagement: true, Now));

        Assert.Equal("publication.active_engagement", exception.Code);
    }

    [Fact]
    public void Update_should_be_blocked_after_status_leaves_open()
    {
        var publication = Publication.Create(Guid.NewGuid(), RequestInput(), Now);
        publication.StartNegotiation(Now);

        var exception = Assert.Throws<DomainException>(
            () => publication.Update(RequestInput(), hasActiveEngagement: false, Now));

        Assert.Equal("publication.not_editable", exception.Code);
    }

    [Fact]
    public void Cancel_should_only_be_allowed_from_open()
    {
        var publication = Publication.Create(Guid.NewGuid(), RequestInput(), Now);
        publication.StartNegotiation(Now);

        var exception = Assert.Throws<DomainException>(() => publication.Cancel(Now));

        Assert.Equal("publication.not_cancellable", exception.Code);
    }

    [Fact]
    public void Status_machine_should_follow_open_negotiation_agreed_completed()
    {
        var publication = Publication.Create(Guid.NewGuid(), RequestInput(), Now);

        publication.StartNegotiation(Now);
        Assert.Equal(PublicationStatus.InNegotiation, publication.Status);

        publication.MarkAgreed(Now);
        Assert.Equal(PublicationStatus.Agreed, publication.Status);

        publication.Complete(Now);
        Assert.Equal(PublicationStatus.Completed, publication.Status);
        Assert.NotNull(publication.ClosedAt);
    }

    [Fact]
    public void Agree_should_fail_when_publication_is_still_open()
    {
        var publication = Publication.Create(Guid.NewGuid(), RequestInput(), Now);

        var exception = Assert.Throws<DomainException>(() => publication.MarkAgreed(Now));

        Assert.Equal("publication.invalid_transition", exception.Code);
    }

    [Fact]
    public void Hide_should_require_note_and_reject_completed_publications()
    {
        var publication = Publication.Create(Guid.NewGuid(), RequestInput(), Now);
        publication.StartNegotiation(Now);
        publication.MarkAgreed(Now);
        publication.Complete(Now);

        var exception = Assert.Throws<DomainException>(() => publication.Hide("Conteúdo impróprio", Now));

        Assert.Equal("publication.not_hideable", exception.Code);
    }
}

public sealed class NegotiationDomainTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 10, 12, 0, 0, TimeSpan.Zero);

    private static Publication CreatePublication(Guid authorId, PublicationType type) =>
        Publication.Create(
            authorId,
            new PublicationInput(
                type,
                Guid.NewGuid(),
                "Preciso de eletricista",
                "Troca de disjuntor e revisão da fiação antiga do imóvel.",
                "São Paulo",
                "Centro",
                "Zona Central",
                type == PublicationType.Request ? 100m : null,
                type == PublicationType.Request ? 300m : null,
                type == PublicationType.Offer ? 250m : null,
                type == PublicationType.Request ? Now.AddDays(3) : null),
            Now);

    [Fact]
    public void Offer_publication_should_have_author_as_provider()
    {
        var authorId = Guid.NewGuid();
        var publication = CreatePublication(authorId, PublicationType.Offer);
        var proposal = Proposal.Create(publication.Id, Guid.NewGuid(), "Tenho interesse no serviço.", 200m, Now);

        publication.StartNegotiation(Now);
        proposal.Accept(Now);
        var engagement = Engagement.Create(publication, proposal, Now);

        Assert.Equal(authorId, engagement.ProviderId);
        Assert.Equal(authorId, engagement.ReceiverId);
        Assert.Equal(proposal.ProposerId, engagement.ProposerId);
    }

    [Fact]
    public void Request_publication_should_have_proposer_as_provider()
    {
        var authorId = Guid.NewGuid();
        var publication = CreatePublication(authorId, PublicationType.Request);
        var proposal = Proposal.Create(publication.Id, Guid.NewGuid(), "Posso executar o serviço.", 200m, Now);

        publication.StartNegotiation(Now);
        proposal.Accept(Now);
        var engagement = Engagement.Create(publication, proposal, Now);

        Assert.Equal(proposal.ProposerId, engagement.ProviderId);
        Assert.Equal(authorId, engagement.ReceiverId);
    }

    [Fact]
    public void Proposal_should_not_be_decided_twice()
    {
        var proposal = Proposal.Create(Guid.NewGuid(), Guid.NewGuid(), "Posso executar o serviço.", 200m, Now);
        proposal.Accept(Now);

        var exception = Assert.Throws<DomainException>(() => proposal.Reject(Now));

        Assert.Equal("proposal.not_pending", exception.Code);
    }

    [Fact]
    public void Review_should_require_completed_publication_and_engagement()
    {
        var authorId = Guid.NewGuid();
        var publication = CreatePublication(authorId, PublicationType.Request);
        var proposal = Proposal.Create(publication.Id, Guid.NewGuid(), "Posso executar o serviço.", 200m, Now);

        publication.StartNegotiation(Now);
        proposal.Accept(Now);
        var engagement = Engagement.Create(publication, proposal, Now);

        var exception = Assert.Throws<DomainException>(
            () => Review.Create(publication, engagement, proposal.ProposerId, authorId, 5, "Ótimo", Now));

        Assert.Equal("review.publication_not_completed", exception.Code);
    }

    [Fact]
    public void Review_should_block_self_review()
    {
        var authorId = Guid.NewGuid();
        var publication = CreatePublication(authorId, PublicationType.Request);
        var proposal = Proposal.Create(publication.Id, Guid.NewGuid(), "Posso executar o serviço.", 200m, Now);

        publication.StartNegotiation(Now);
        proposal.Accept(Now);
        var engagement = Engagement.Create(publication, proposal, Now);
        publication.MarkAgreed(Now);
        publication.Complete(Now);
        engagement.Complete(Now);

        var exception = Assert.Throws<DomainException>(
            () => Review.Create(publication, engagement, proposal.ProposerId, proposal.ProposerId, 5, null, Now));

        Assert.Equal("review.self", exception.Code);
    }

    [Fact]
    public void Review_should_block_non_participant()
    {
        var authorId = Guid.NewGuid();
        var publication = CreatePublication(authorId, PublicationType.Request);
        var proposal = Proposal.Create(publication.Id, Guid.NewGuid(), "Posso executar o serviço.", 200m, Now);

        publication.StartNegotiation(Now);
        proposal.Accept(Now);
        var engagement = Engagement.Create(publication, proposal, Now);
        publication.MarkAgreed(Now);
        publication.Complete(Now);
        engagement.Complete(Now);

        var exception = Assert.Throws<DomainException>(
            () => Review.Create(publication, engagement, Guid.NewGuid(), authorId, 5, null, Now));

        Assert.Equal("review.not_participant", exception.Code);
    }

    [Fact]
    public void Review_should_block_out_of_range_rating()
    {
        var authorId = Guid.NewGuid();
        var publication = CreatePublication(authorId, PublicationType.Request);
        var proposal = Proposal.Create(publication.Id, Guid.NewGuid(), "Posso executar o serviço.", 200m, Now);

        publication.StartNegotiation(Now);
        proposal.Accept(Now);
        var engagement = Engagement.Create(publication, proposal, Now);
        publication.MarkAgreed(Now);
        publication.Complete(Now);
        engagement.Complete(Now);

        var exception = Assert.Throws<DomainException>(
            () => Review.Create(publication, engagement, proposal.ProposerId, authorId, 6, null, Now));

        Assert.Equal("review.rating", exception.Code);
    }
}

public sealed class ReportDomainTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 10, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Report_should_require_a_target()
    {
        var exception = Assert.Throws<DomainException>(
            () => Report.Create(Guid.NewGuid(), null, null, ReportReason.Spam, null, Now));

        Assert.Equal("report.target_required", exception.Code);
    }

    [Fact]
    public void Report_should_block_self_report()
    {
        var userId = Guid.NewGuid();

        var exception = Assert.Throws<DomainException>(
            () => Report.Create(userId, userId, null, ReportReason.Fraud, null, Now));

        Assert.Equal("report.self", exception.Code);
    }

    [Fact]
    public void Report_should_start_pending_and_not_hide_content()
    {
        var report = Report.Create(Guid.NewGuid(), Guid.NewGuid(), null, ReportReason.Spam, "Perfil suspeito", Now);

        Assert.Equal(ReportStatus.Pending, report.Status);
        Assert.Null(report.ResolvedAt);
    }
}
