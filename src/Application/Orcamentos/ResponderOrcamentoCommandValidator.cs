using FluentValidation;

namespace Fiap.TechChallenge.OficinaMecanica.Billing.Application.Orcamentos;

public sealed class ResponderOrcamentoCommandValidator
    : AbstractValidator<ResponderOrcamentoCommand>
{
    public ResponderOrcamentoCommandValidator()
    {
        RuleFor(command => command.OrdemServicoId).GreaterThan(0);
        RuleFor(command => command.OrcamentoId).NotEmpty();
        RuleFor(command => command.Motivo)
            .NotEmpty()
            .MaximumLength(500)
            .When(command => !command.Aprovado);
    }
}
