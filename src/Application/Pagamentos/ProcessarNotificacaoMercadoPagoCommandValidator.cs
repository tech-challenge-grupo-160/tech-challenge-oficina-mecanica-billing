using FluentValidation;

namespace Fiap.TechChallenge.OficinaMecanica.Billing.Application.Pagamentos;

public sealed class ProcessarNotificacaoMercadoPagoCommandValidator
    : AbstractValidator<ProcessarNotificacaoMercadoPagoCommand>
{
    public ProcessarNotificacaoMercadoPagoCommandValidator() =>
        RuleFor(command => command.MercadoPagoPagamentoId)
            .NotEmpty()
            .MaximumLength(64)
            .Matches("^[A-Za-z0-9_-]+$");
}
