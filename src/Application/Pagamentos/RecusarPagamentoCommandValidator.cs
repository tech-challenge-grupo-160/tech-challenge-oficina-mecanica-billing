using FluentValidation;

namespace Fiap.TechChallenge.OficinaMecanica.Billing.Application.Pagamentos;

public sealed class RecusarPagamentoCommandValidator : AbstractValidator<RecusarPagamentoCommand>
{
    public RecusarPagamentoCommandValidator()
    {
        RuleFor(command => command.OrdemServicoId).GreaterThan(0);
        RuleFor(command => command.PagamentoId).NotEmpty();
        RuleFor(command => command.Motivo).NotEmpty().MaximumLength(200);
    }
}
