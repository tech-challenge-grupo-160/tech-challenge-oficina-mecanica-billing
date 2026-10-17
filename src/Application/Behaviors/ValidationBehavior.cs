using Fiap.TechChallenge.OficinaMecanica.Billing.Application.Exceptions;
using FluentValidation;
using MediatR;

namespace Fiap.TechChallenge.OficinaMecanica.Billing.Application.Behaviors;

public sealed class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!_validators.Any())
        {
            return await next();
        }

        var context = new ValidationContext<TRequest>(request);
        var validationResults = await Task.WhenAll(
            _validators.Select(validator => validator.ValidateAsync(context, cancellationToken)));

        var errors = validationResults
            .SelectMany(result => result.Errors)
            .Where(error => error is not null)
            .ToArray();

        if (errors.Length > 0)
        {
            throw new ServiceValidationException(
                string.Join("; ", errors.Select(error => error.ErrorMessage)));
        }

        return await next();
    }
}
