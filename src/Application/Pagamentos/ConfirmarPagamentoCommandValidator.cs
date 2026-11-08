using FluentValidation;

namespace Fiap.TechChallenge.OficinaMecanica.Billing.Application.Pagamentos;

public sealed class ConfirmarPagamentoCommandValidator : AbstractValidator<ConfirmarPagamentoCommand>
{
    public ConfirmarPagamentoCommandValidator()
    {
        RuleFor(command => command.OrdemServicoId).GreaterThan(0);
        RuleFor(command => command.PagamentoId).NotEmpty();
    }
}
