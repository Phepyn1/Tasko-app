using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Tasko.Domain;

namespace Tasko.Api.Middleware;

/// <summary>RNF10: respostas padronizadas (RFC 7807) sem vazar stack trace.</summary>
public sealed class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (DomainException exception)
        {
            var (status, title) = Map(exception.Kind);
            await WriteProblemAsync(context, status, title, exception.Message, exception.Code);
        }
        catch (ValidationException exception)
        {
            var errors = exception.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());

            var problem = new ValidationProblemDetails(errors)
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Dados inválidos",
                Detail = "Um ou mais campos estão inválidos.",
                Instance = context.Request.Path
            };
            problem.Extensions["code"] = "validation.failed";
            problem.Extensions["traceId"] = context.TraceIdentifier;

            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsJsonAsync(problem, context.RequestAborted);
        }
        catch (DbUpdateException exception)
        {
            logger.LogError(exception, "Falha de persistência em {Path}", context.Request.Path);
            await WriteProblemAsync(
                context,
                StatusCodes.Status409Conflict,
                "Conflito de dados",
                "Não foi possível concluir a operação por conflito de dados.",
                "db.conflict");
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Erro não tratado em {Path}", context.Request.Path);
            await WriteProblemAsync(
                context,
                StatusCodes.Status500InternalServerError,
                "Erro interno",
                "Ocorreu um erro inesperado. Informe o identificador de rastreio ao suporte.",
                "internal.error");
        }
    }

    private static (int Status, string Title) Map(ErrorKind kind) => kind switch
    {
        ErrorKind.Validation => (StatusCodes.Status400BadRequest, "Requisição inválida"),
        ErrorKind.Unauthorized => (StatusCodes.Status401Unauthorized, "Não autenticado"),
        ErrorKind.Forbidden => (StatusCodes.Status403Forbidden, "Acesso negado"),
        ErrorKind.NotFound => (StatusCodes.Status404NotFound, "Não encontrado"),
        ErrorKind.Conflict => (StatusCodes.Status409Conflict, "Conflito de estado"),
        _ => (StatusCodes.Status400BadRequest, "Requisição inválida")
    };

    private static async Task WriteProblemAsync(
        HttpContext context,
        int status,
        string title,
        string detail,
        string code)
    {
        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail,
            Instance = context.Request.Path
        };
        problem.Extensions["code"] = code;
        problem.Extensions["traceId"] = context.TraceIdentifier;

        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(problem, context.RequestAborted);
    }
}
