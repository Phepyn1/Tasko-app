using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tasko.Application.Contracts;
using Tasko.Application.Services;

namespace Tasko.Api.Controllers;

[ApiController]
[Route("api/v1/categories")]
public sealed class CategoriesController(CategoryService categories) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public Task<IReadOnlyList<CategoryResponse>> List(CancellationToken ct) => categories.ListAsync(ct);
}
