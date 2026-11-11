using FluentValidation;

namespace Fiap.TechChallenge.OficinaMecanica.Billing.Application.Pagamentos;

public sealed class ObterPagamentoPorOrdemQueryValidator : AbstractValidator<ObterPagamentoPorOrdemQuery>
{
    public ObterPagamentoPorOrdemQueryValidator() =>
        RuleFor(query => query.OrdemServicoId).GreaterThan(0);
}
