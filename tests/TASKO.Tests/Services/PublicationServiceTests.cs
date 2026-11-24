using Tasko.Application.Contracts;
using Tasko.Domain;

namespace Tasko.Tests.Services;

public sealed class PublicationServiceTests
{
    [Fact]
    public async Task Update_should_be_blocked_after_accept()
    {
        await using var h = new TestHarness();
        var author = await h.CreateUserAsync("Autor", "autor@teste.com", RoleType.Requester);
        var provider = await h.CreateUserAsync("Prestador", "p1@teste.com", RoleType.Provider);
        var publication = await h.CreatePublicationAsync(author, PublicationType.Request);
        var proposal = await h.CreateProposalAsync(publication, provider);

        await h.AcceptAsync(proposal, author);

        h.ActAs(author);
        var request = new UpdatePublicationRequest(
            publication.CategoryId,
            "Novo título da publicação",
            "Nova descrição completa do serviço solicitado.",
            "São Paulo",
            "Centro",
            "Zona Central",
            100m,
            300m,
            null,
            h.Clock.UtcNow.AddDays(5));

        var exception = await Assert.ThrowsAsync<DomainException>(
            () => h.Publications.UpdateAsync(publication.Id, request, CancellationToken.None));

        Assert.Equal("publication.not_editable", exception.Code);
    }

    [Fact]
    public async Task Search_should_filter_exclude_hidden_and_paginate()
    {
        await using var h = new TestHarness();
        var author = await h.CreateUserAsync("Autor", "autor@teste.com", RoleType.Requester, RoleType.Provider);
        var electrical = await h.CreateCategoryAsync("Elétrica");
        var cleaning = await h.CreateCategoryAsync("Limpeza");

        await h.CreatePublicationAsync(author, PublicationType.Request, "São Paulo", category: electrical);
        await h.CreatePublicationAsync(author, PublicationType.Request, "Campinas", category: electrical);
        await h.CreatePublicationAsync(author, PublicationType.Offer, "São Paulo", category: cleaning);
        var hidden = await h.CreatePublicationAsync(author, PublicationType.Request, "São Paulo", category: electrical);
        hidden.Hide("Conteúdo impróprio", h.Clock.UtcNow);
        await h.Db.SaveChangesAsync();

        h.ActAs(null);

        var byCity = await h.Publications.SearchAsync(
            null, null, "são paulo", null, null, null, 1, 10, CancellationToken.None);
        Assert.Equal(2, byCity.Total);

        var byType = await h.Publications.SearchAsync(
            PublicationType.Request, null, null, null, null, null, 1, 10, CancellationToken.None);
        Assert.Equal(2, byType.Total);

        var byCategory = await h.Publications.SearchAsync(
            null, cleaning.Id, null, null, null, null, 1, 10, CancellationToken.None);
        Assert.Single(byCategory.Items);

        var paged = await h.Publications.SearchAsync(
            null, null, null, null, null, null, 1, 1, CancellationToken.None);
        Assert.Single(paged.Items);
        Assert.Equal(3, paged.Total);
        Assert.Equal(3, paged.TotalPages);
    }

    [Fact]
    public async Task Hidden_publication_should_be_invisible_to_anonymous_users()
    {
        await using var h = new TestHarness();
        var author = await h.CreateUserAsync("Autor", "autor@teste.com", RoleType.Requester);
        var publication = await h.CreatePublicationAsync(author, PublicationType.Request);
        publication.Hide("Conteúdo impróprio", h.Clock.UtcNow);
        await h.Db.SaveChangesAsync();

        h.ActAs(null);

        var exception = await Assert.ThrowsAsync<DomainException>(
            () => h.Publications.GetAsync(publication.Id, CancellationToken.None));
        Assert.Equal("publication.not_found", exception.Code);

        h.ActAs(author);
        var detail = await h.Publications.GetAsync(publication.Id, CancellationToken.None);
        Assert.Equal(PublicationStatus.Hidden, detail.Publication.Status);
    }

    [Fact]
    public async Task Cancel_should_record_status_history()
    {
        await using var h = new TestHarness();
        var author = await h.CreateUserAsync("Autor", "autor@teste.com", RoleType.Requester);
        var publication = await h.CreatePublicationAsync(author, PublicationType.Request);

        h.ActAs(author);
        await h.Publications.CancelAsync(publication.Id, CancellationToken.None);

        var detail = await h.Publications.GetAsync(publication.Id, CancellationToken.None);
        Assert.Equal(PublicationStatus.Cancelled, detail.Publication.Status);
        Assert.Contains(detail.StatusHistory, x => x.NewStatus == PublicationStatus.Cancelled);
    }

    [Fact]
    public async Task Only_author_can_update_publication()
    {
        await using var h = new TestHarness();
        var author = await h.CreateUserAsync("Autor", "autor@teste.com", RoleType.Requester);
        var other = await h.CreateUserAsync("Outro", "outro@teste.com", RoleType.Provider);
        var publication = await h.CreatePublicationAsync(author, PublicationType.Request);

        h.ActAs(other);
        var request = new UpdatePublicationRequest(
            publication.CategoryId,
            "Tentativa de alteração",
            "Tentando alterar a publicação de outra pessoa.",
            "São Paulo",
            "Centro",
            "Zona Central",
            100m,
            300m,
            null,
            h.Clock.UtcNow.AddDays(5));

        var exception = await Assert.ThrowsAsync<DomainException>(
            () => h.Publications.UpdateAsync(publication.Id, request, CancellationToken.None));

        Assert.Equal("publication.not_author", exception.Code);
    }
}
