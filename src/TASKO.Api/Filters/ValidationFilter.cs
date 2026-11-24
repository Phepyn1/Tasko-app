using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Tasko.Api.Filters;

/// <summary>Executa o IValidator&lt;T&gt; registrado para cada argumento da action.</summary>
public sealed class ValidationFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument is null)
            {
                continue;
            }

            var validatorType = typeof(IValidator<>).MakeGenericType(argument.GetType());
            if (context.HttpContext.RequestServices.GetService(validatorType) is not IValidator validator)
            {
                continue;
            }

            var validationContext = new ValidationContext<object>(argument);
            var result = await validator.ValidateAsync(validationContext, context.HttpContext.RequestAborted);
            if (result.IsValid)
            {
                continue;
            }

            var errors = result.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());

            var problem = new ValidationProblemDetails(errors)
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Dados inválidos",
                Detail = "Um ou mais campos estão inválidos.",
                Instance = context.HttpContext.Request.Path
            };
            problem.Extensions["code"] = "validation.failed";
            problem.Extensions["traceId"] = context.HttpContext.TraceIdentifier;

            context.Result = new ObjectResult(problem) { StatusCode = StatusCodes.Status400BadRequest };
            return;
        }

        await next();
    }
}
