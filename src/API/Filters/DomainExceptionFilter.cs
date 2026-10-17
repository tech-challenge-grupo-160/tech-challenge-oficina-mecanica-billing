using Fiap.TechChallenge.OficinaMecanica.Billing.Application.Exceptions;
using Fiap.TechChallenge.OficinaMecanica.Billing.API.ProblemDetails;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Fiap.TechChallenge.OficinaMecanica.Billing.API.Filters;

public class DomainExceptionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        var exception = context.Exception;

        if (exception is IServiceExceptionContract serviceException)
        {
            context.Result = ApiProblemDetails.CreateResult(
                context.HttpContext,
                serviceException.StatusCode,
                exception.Message);
            context.ExceptionHandled = true;
            return;
        }

        if (exception is KeyNotFoundException)
        {
            context.Result = ApiProblemDetails.CreateResult(
                context.HttpContext,
                StatusCodes.Status404NotFound,
                exception.Message);
            context.ExceptionHandled = true;
            return;
        }

        if (exception is ArgumentException or InvalidOperationException)
        {
            context.Result = ApiProblemDetails.CreateResult(
                context.HttpContext,
                StatusCodes.Status400BadRequest,
                exception.Message);
            context.ExceptionHandled = true;
            return;
        }

        if (exception is UnauthorizedAccessException)
        {
            context.Result = ApiProblemDetails.CreateResult(
                context.HttpContext,
                StatusCodes.Status401Unauthorized,
                exception.Message);
            context.ExceptionHandled = true;
            return;
        }

        context.Result = ApiProblemDetails.CreateResult(
            context.HttpContext,
            StatusCodes.Status500InternalServerError,
            "Erro interno no servidor.");
        context.ExceptionHandled = true;
    }
}
