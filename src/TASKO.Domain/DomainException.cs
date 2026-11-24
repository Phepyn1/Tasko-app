namespace Tasko.Domain;

public enum ErrorKind
{
    Validation,
    Unauthorized,
    Forbidden,
    NotFound,
    Conflict
}

/// <summary>
/// Erro de regra de negócio. A camada de API traduz <see cref="Kind"/> em status HTTP
/// e <see cref="Code"/> em uma extensão estável do ProblemDetails.
/// </summary>
public sealed class DomainException : Exception
{
    public string Code { get; }
    public ErrorKind Kind { get; }

    public DomainException(string code, string message, ErrorKind kind = ErrorKind.Validation)
        : base(message)
    {
        Code = code;
        Kind = kind;
    }

    public static DomainException Validation(string code, string message) => new(code, message, ErrorKind.Validation);
    public static DomainException Unauthorized(string code, string message) => new(code, message, ErrorKind.Unauthorized);
    public static DomainException Forbidden(string code, string message) => new(code, message, ErrorKind.Forbidden);
    public static DomainException NotFound(string code, string message) => new(code, message, ErrorKind.NotFound);
    public static DomainException Conflict(string code, string message) => new(code, message, ErrorKind.Conflict);
}

internal static class Guard
{
    public static string Required(string? value, string label, int min, int max, string code)
    {
        var trimmed = (value ?? string.Empty).Trim();
        if (trimmed.Length < min || trimmed.Length > max)
        {
            throw DomainException.Validation(code, $"{label} deve ter entre {min} e {max} caracteres.");
        }

        return trimmed;
    }

    public static string? Optional(string? value, string label, int max, string code)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        if (trimmed.Length > max)
        {
            throw DomainException.Validation(code, $"{label} deve ter no máximo {max} caracteres.");
        }

        return trimmed;
    }

    public static Guid RequiredId(Guid value, string label, string code)
    {
        if (value == Guid.Empty)
        {
            throw DomainException.Validation(code, $"{label} é obrigatório.");
        }

        return value;
    }

    public static decimal? NonNegative(decimal? value, string label, string code)
    {
        if (value is < 0)
        {
            throw DomainException.Validation(code, $"{label} não pode ser negativo.");
        }

        return value;
    }
}
