using Tasko.Application.Contracts;
using Tasko.Domain;
using Xunit;

namespace Tasko.Tests;

public sealed class ProfileServiceTests
{
    [Fact]
    public async Task ReplaceSkills_substitui_habilidades_do_usuario()
    {
        await using var harness = new TestHarness();
        var user = await harness.CreateUserAsync("Carlos", "carlos@teste.com", RoleType.Provider);
        var eletrica = await harness.CreateCategoryAsync("Elétrica");
        var limpeza = await harness.CreateCategoryAsync("Limpeza");
        harness.ActAs(user);

        var first = await harness.Profiles.ReplaceSkillsAsync(
            new UpdateSkillsRequest([new SkillItem(eletrica.Id, "Instalação elétrica")]),
            CancellationToken.None);

        Assert.Single(first.Skills);
        Assert.Equal("Instalação elétrica", first.Skills[0].SkillName);
        Assert.Equal("Elétrica", first.Skills[0].CategoryName);

        var second = await harness.Profiles.ReplaceSkillsAsync(
            new UpdateSkillsRequest([new SkillItem(limpeza.Id, "Limpeza residencial")]),
            CancellationToken.None);

        Assert.Single(second.Skills);
        Assert.Equal("Limpeza residencial", second.Skills[0].SkillName);
        Assert.DoesNotContain(second.Skills, s => s.SkillName == "Instalação elétrica");
    }
}
