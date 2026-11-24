using Microsoft.EntityFrameworkCore;
using Tasko.Application.Contracts;
using Tasko.Domain;
using Tasko.Domain.Entities;

namespace Tasko.Tests.Services;

public sealed class ProposalFlowTests
{
    [Fact]
    public async Task Accept_should_be_atomic_reject_others_and_notify_proposer()
    {
        await using var h = new TestHarness();
        var author = await h.CreateUserAsync("Autor", "autor@teste.com", RoleType.Requester);
        var provider1 = await h.CreateUserAsync("Prestador Um", "p1@teste.com", RoleType.Provider);
        var provider2 = await h.CreateUserAsync("Prestador Dois", "p2@teste.com", RoleType.Provider);
        var publication = await h.CreatePublicationAsync(author, PublicationType.Request);
        var proposal1 = await h.CreateProposalAsync(publication, provider1);
        var proposal2 = await h.CreateProposalAsync(publication, provider2);

        var engagement = await h.AcceptAsync(proposal1, author);

        var storedPublication = await h.Db.Publications.FindAsync(publication.Id);
        var storedProposal1 = await h.Db.Proposals.FindAsync(proposal1.Id);
        var storedProposal2 = await h.Db.Proposals.FindAsync(proposal2.Id);

        Assert.Equal(PublicationStatus.InNegotiation, storedPublication!.Status);
        Assert.Equal(ProposalStatus.Accepted, storedProposal1!.Status);
        Assert.Equal(ProposalStatus.Rejected, storedProposal2!.Status);
        Assert.Equal(EngagementStatus.Active, engagement.Status);
        Assert.NotNull(engagement.ContactRevealedAt);
        Assert.Equal(provider1.Id, engagement.ProposerId);
        Assert.Equal(author.Id, engagement.ReceiverId);
        Assert.Equal(provider1.Id, engagement.ProviderId);

        Assert.True(await h.Db.PublicationStatusHistories.AnyAsync(
            x => x.PublicationId == publication.Id && x.NewStatus == PublicationStatus.InNegotiation));
        Assert.True(await h.Db.Notifications.AnyAsync(
            n => n.UserId == provider1.Id && n.Type == NotificationType.ProposalAccepted));
        Assert.True(await h.Db.ActivityLogs.AnyAsync(a => a.Action == "proposal.accept"));
    }

    [Fact]
    public async Task Only_publication_author_can_accept_proposals()
    {
        await using var h = new TestHarness();
        var author = await h.CreateUserAsync("Autor", "autor@teste.com", RoleType.Requester);
        var provider = await h.CreateUserAsync("Prestador", "p1@teste.com", RoleType.Provider);
        var publication = await h.CreatePublicationAsync(author, PublicationType.Request);
        var proposal = await h.CreateProposalAsync(publication, provider);

        var exception = await Assert.ThrowsAsync<DomainException>(() => h.AcceptAsync(proposal, provider));

        Assert.Equal("proposal.not_author", exception.Code);
    }

    [Fact]
    public async Task Accept_should_fail_when_publication_is_not_open()
    {
        await using var h = new TestHarness();
        var author = await h.CreateUserAsync("Autor", "autor@teste.com", RoleType.Requester);
        var provider = await h.CreateUserAsync("Prestador", "p1@teste.com", RoleType.Provider);
        var publication = await h.CreatePublicationAsync(author, PublicationType.Request);
        var proposal = await h.CreateProposalAsync(publication, provider);

        h.ActAs(author);
        await h.Publications.CancelAsync(publication.Id, CancellationToken.None);

        var exception = await Assert.ThrowsAsync<DomainException>(() => h.AcceptAsync(proposal, author));

        Assert.Equal("publication.not_open", exception.Code);
    }

    [Fact]
    public async Task Contact_should_be_visible_only_to_participants_after_accept()
    {
        await using var h = new TestHarness();
        var author = await h.CreateUserAsync("Autor", "autor@teste.com", RoleType.Requester);
        var provider = await h.CreateUserAsync("Prestador", "p1@teste.com", RoleType.Provider);
        var outsider = await h.CreateUserAsync("Intruso", "intruso@teste.com", RoleType.Provider);

        author.Profile!.Update(null, "11999998888", "São Paulo", null, null, "Zona Central", null, h.Clock.UtcNow);
        provider.Profile!.Update(null, "11888887777", "São Paulo", null, null, "Zona Central", null, h.Clock.UtcNow);
        await h.Db.SaveChangesAsync();

        var publication = await h.CreatePublicationAsync(author, PublicationType.Request);
        var proposal = await h.CreateProposalAsync(publication, provider);
        var engagement = await h.AcceptAsync(proposal, author);

        h.ActAs(provider);
        var contact = await h.Engagements.GetContactAsync(engagement.Id, CancellationToken.None);

        Assert.Equal("11888887777", contact.Proposer.Phone);
        Assert.Equal("11999998888", contact.Receiver.Phone);
        Assert.Equal("autor@teste.com", contact.Receiver.Email);
        Assert.Equal("p1@teste.com", contact.Proposer.Email);

        h.ActAs(outsider);
        var exception = await Assert.ThrowsAsync<DomainException>(
            () => h.Engagements.GetContactAsync(engagement.Id, CancellationToken.None));
        Assert.Equal("engagement.forbidden", exception.Code);
    }

    [Fact]
    public async Task Request_publication_requires_provider_role_to_propose()
    {
        await using var h = new TestHarness();
        var author = await h.CreateUserAsync("Autor", "autor@teste.com", RoleType.Requester);
        var requester = await h.CreateUserAsync("Solicitante", "sol@teste.com", RoleType.Requester);
        var publication = await h.CreatePublicationAsync(author, PublicationType.Request);

        var exception = await Assert.ThrowsAsync<DomainException>(
            () => h.CreateProposalAsync(publication, requester));

        Assert.Equal("proposal.provider_required", exception.Code);
    }

    [Fact]
    public async Task Offer_publication_accepts_proposal_from_requester()
    {
        await using var h = new TestHarness();
        var author = await h.CreateUserAsync("Prestador Autor", "autor@teste.com", RoleType.Provider);
        var requester = await h.CreateUserAsync("Solicitante", "sol@teste.com", RoleType.Requester);
        var publication = await h.CreatePublicationAsync(author, PublicationType.Offer);

        var proposal = await h.CreateProposalAsync(publication, requester);

        Assert.Equal(ProposalStatus.Pending, proposal.Status);
        Assert.Equal(requester.Id, proposal.ProposerId);
    }

    [Fact]
    public async Task Author_cannot_propose_on_own_publication()
    {
        await using var h = new TestHarness();
        var author = await h.CreateUserAsync(
            "Prestador Autor", "autor@teste.com", RoleType.Provider, RoleType.Requester);
        var publication = await h.CreatePublicationAsync(author, PublicationType.Offer);

        var exception = await Assert.ThrowsAsync<DomainException>(
            () => h.CreateProposalAsync(publication, author));

        Assert.Equal("proposal.self", exception.Code);
    }

    [Fact]
    public async Task Duplicate_pending_proposal_should_be_blocked()
    {
        await using var h = new TestHarness();
        var author = await h.CreateUserAsync("Autor", "autor@teste.com", RoleType.Requester);
        var provider = await h.CreateUserAsync("Prestador", "p1@teste.com", RoleType.Provider);
        var publication = await h.CreatePublicationAsync(author, PublicationType.Request);

        await h.CreateProposalAsync(publication, provider);
        var exception = await Assert.ThrowsAsync<DomainException>(
            () => h.CreateProposalAsync(publication, provider));

        Assert.Equal("proposal.duplicate", exception.Code);
    }
}
