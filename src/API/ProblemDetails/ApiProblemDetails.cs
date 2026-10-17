using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Fiap.TechChallenge.OficinaMecanica.Billing.API.ProblemDetails;

public static class ApiProblemDetails
{
    public const string ContentType = "application/problem+json";

    public static Microsoft.AspNetCore.Mvc.ProblemDetails Create(
        HttpContext httpContext,
        int statusCode,
        string detail)
    {
        var problemDetails = new Microsoft.AspNetCore.Mvc.ProblemDetails
        {
            Status = statusCode,
            Title = GetTitle(statusCode),
            Detail = detail,
            Instance = httpContext.Request.Path
        };

        AddTraceId(problemDetails, httpContext);
        return problemDetails;
    }

    public static ValidationProblemDetails CreateValidation(
        HttpContext httpContext,
        ModelStateDictionary modelState)
    {
        var problemDetails = new ValidationProblemDetails(modelState)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = GetTitle(StatusCodes.Status400BadRequest),
            Detail = "A requisicao possui dados invalidos.",
            Instance = httpContext.Request.Path
        };

        AddTraceId(problemDetails, httpContext);
        return problemDetails;
    }

    public static ObjectResult CreateResult(
        HttpContext httpContext,
        int statusCode,
        string detail)
    {
        var result = new ObjectResult(Create(httpContext, statusCode, detail))
        {
            StatusCode = statusCode
        };
        result.ContentTypes.Add(ContentType);

        return result;
    }

    private static void AddTraceId(Microsoft.AspNetCore.Mvc.ProblemDetails problemDetails, HttpContext httpContext)
    {
        problemDetails.Extensions["traceId"] = Activity.Current?.TraceId.ToString()
            ?? httpContext.TraceIdentifier;
    }

    private static string GetTitle(int statusCode)
    {
        return statusCode switch
        {
            StatusCodes.Status400BadRequest => "Requisicao invalida.",
            StatusCodes.Status401Unauthorized => "Nao autorizado.",
            StatusCodes.Status404NotFound => "Recurso nao encontrado.",
            StatusCodes.Status500InternalServerError => "Erro interno no servidor.",
            _ => "Erro ao processar a requisicao."
        };
    }
}
