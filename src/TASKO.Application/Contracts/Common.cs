namespace Tasko.Application.Contracts;

public sealed record MessageResponse(string Message);

public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total)
{
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(Total / (double)PageSize);
}
