using FluentValidation;

namespace Fiap.TechChallenge.OficinaMecanica.Billing.Application.Pagamentos;

public sealed class SolicitarPagamentoCommandValidator : AbstractValidator<SolicitarPagamentoCommand>
{
    public SolicitarPagamentoCommandValidator()
    {
        RuleFor(command => command.MensagemEntradaId).NotEmpty();
        RuleFor(command => command.CorrelationId).NotEmpty();
        RuleFor(command => command.OrcamentoId).NotEmpty();
        RuleFor(command => command.OrdemServicoId).GreaterThan(0);
        RuleFor(command => command.Tentativa).GreaterThan(0);
    }
}
