using FluentValidation;

namespace Fiap.TechChallenge.OficinaMecanica.Billing.Application.Orcamentos;

public sealed class ObterOrcamentoPorOrdemQueryValidator : AbstractValidator<ObterOrcamentoPorOrdemQuery>
{
    public ObterOrcamentoPorOrdemQueryValidator()
    {
        RuleFor(query => query.OrdemServicoId).GreaterThan(0);
    }
}
