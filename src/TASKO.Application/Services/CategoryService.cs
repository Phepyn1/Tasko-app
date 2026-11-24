using Tasko.Application.Contracts;
using Tasko.Domain.Repositories;

namespace Tasko.Application.Services;

public sealed class CategoryService(ICategoryRepository categories)
{
    public async Task<IReadOnlyList<CategoryResponse>> ListAsync(CancellationToken ct)
    {
        var items = await categories.ListAsync(ct);
        return items.Select(c => new CategoryResponse(c.Id, c.Name, c.Description)).ToList();
    }
}
