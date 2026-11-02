using FluentValidation;

namespace Fiap.TechChallenge.OficinaMecanica.Billing.Application.Orcamentos;

public sealed class GerarOrcamentoCommandValidator : AbstractValidator<GerarOrcamentoCommand>
{
    public GerarOrcamentoCommandValidator()
    {
        RuleFor(command => command.MensagemEntradaId).NotEmpty();
        RuleFor(command => command.CorrelationId).NotEmpty();
        RuleFor(command => command.OrdemServicoId).GreaterThan(0);
        RuleFor(command => command.DiagnosticoId).GreaterThan(0);
        RuleFor(command => command.ParecerTecnico).NotEmpty().MaximumLength(2000);
        RuleFor(command => command.Servicos).NotEmpty();
        RuleForEach(command => command.Servicos).ChildRules(servico =>
        {
            servico.RuleFor(item => item.ServicoId).GreaterThan(0);
            servico.RuleFor(item => item.Nome).NotEmpty();
            servico.RuleFor(item => item.Preco).GreaterThanOrEqualTo(0);
            servico.RuleFor(item => item.TempoEstimado).GreaterThan(0);
        });
        RuleForEach(command => command.Pecas).ChildRules(peca =>
        {
            peca.RuleFor(item => item.PecaId).GreaterThan(0);
            peca.RuleFor(item => item.Nome).NotEmpty();
            peca.RuleFor(item => item.Quantidade).GreaterThan(0);
            peca.RuleFor(item => item.Preco).GreaterThanOrEqualTo(0);
        });
    }
}
