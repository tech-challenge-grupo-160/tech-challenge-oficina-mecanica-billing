using Fiap.TechChallenge.OficinaMecanica.Billing.Application.Abstractions;
using Fiap.TechChallenge.OficinaMecanica.Billing.Application.Exceptions;
using Fiap.TechChallenge.OficinaMecanica.Billing.Application.Messaging;
using Fiap.TechChallenge.OficinaMecanica.Billing.Application.Pagamentos;
using Fiap.TechChallenge.OficinaMecanica.Billing.Domain.Entities;
using FluentAssertions;
using Moq;

namespace Fiap.TechChallenge.OficinaMecanica.Billing.Test.UnitTests.Units.Application;

public sealed class PagamentoHandlersTests
{
    [Fact]
    public async Task Solicitar_DeveUsarValorDoOrcamentoAprovado()
    {
        var orcamento = CriarOrcamentoAprovado();
        var orcamentos = new Mock<IOrcamentoRepository>();
        var pagamentos = new Mock<IPagamentoRepository>();
        Pagamento? persistido = null;
        orcamentos.Setup(repository => repository.ObterPorOrdemServicoIdAsync(
                3007, It.IsAny<CancellationToken>()))
            .ReturnsAsync(orcamento);
        pagamentos.Setup(repository => repository.CriarOuObterAsync(
                It.IsAny<Pagamento>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Callback<Pagamento, Guid, CancellationToken>((pagamento, _, _) => persistido = pagamento)
            .ReturnsAsync(() => (persistido!, true));
        var command = new SolicitarPagamentoCommand(
            Guid.NewGuid(), orcamento.CorrelationId, orcamento.Id, 3007, DateTimeOffset.UtcNow);

        var result = await new SolicitarPagamentoCommandHandler(orcamentos.Object, pagamentos.Object)
            .Handle(command, CancellationToken.None);

        result.Status.Should().Be("Pendente");
        result.Valor.Should().Be(240m);
        persistido!.OrcamentoId.Should().Be(orcamento.Id);
    }

    [Fact]
    public async Task Solicitar_DeveRejeitarOrcamentoNaoAprovado()
    {
        var orcamento = CriarOrcamento();
        var orcamentos = new Mock<IOrcamentoRepository>();
        orcamentos.Setup(repository => repository.ObterPorOrdemServicoIdAsync(
                3007, It.IsAny<CancellationToken>()))
            .ReturnsAsync(orcamento);

        var action = () => new SolicitarPagamentoCommandHandler(
                orcamentos.Object, Mock.Of<IPagamentoRepository>())
            .Handle(new SolicitarPagamentoCommand(
                Guid.NewGuid(), orcamento.CorrelationId, orcamento.Id, 3007, DateTimeOffset.UtcNow),
                CancellationToken.None);

        await action.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Confirmar_DevePersistirEventoComDataEValor()
    {
        var pagamento = CriarPagamento();
        PagamentoConfirmadoMessage? evento = null;
        var repository = new Mock<IPagamentoRepository>();
        repository.Setup(item => item.ObterAsync(3007, pagamento.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagamento);
        repository.Setup(item => item.ConfirmarAsync(
                pagamento, It.IsAny<PagamentoConfirmadoMessage>(), It.IsAny<CancellationToken>()))
            .Callback<Pagamento, PagamentoConfirmadoMessage, CancellationToken>(
                (_, message, _) => evento = message)
            .ReturnsAsync(() => (pagamento, true));

        var result = await new ConfirmarPagamentoCommandHandler(repository.Object)
            .Handle(new ConfirmarPagamentoCommand(3007, pagamento.Id), CancellationToken.None);

        result.Status.Should().Be("Confirmado");
        evento.Should().NotBeNull();
        evento!.Valor.Should().Be(219.90m);
        evento.ConfirmadoEm.Should().Be(pagamento.ConfirmadoEm);
    }

    [Fact]
    public async Task Obter_DeveRetornarNotFoundQuandoNaoExistir()
    {
        var repository = new Mock<IPagamentoRepository>();
        repository.Setup(item => item.ObterPorOrdemServicoIdAsync(
                3007, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Pagamento?)null);

        var action = () => new ObterPagamentoPorOrdemQueryHandler(repository.Object)
            .Handle(new ObterPagamentoPorOrdemQuery(3007), CancellationToken.None);

        await action.Should().ThrowAsync<ServiceNotFoundException>();
    }

    [Fact]
    public void Validators_DevemRejeitarIdentificadoresInvalidos()
    {
        new SolicitarPagamentoCommandValidator().Validate(new SolicitarPagamentoCommand(
            Guid.Empty, Guid.Empty, Guid.Empty, 0, DateTimeOffset.UtcNow)).IsValid.Should().BeFalse();
        new ConfirmarPagamentoCommandValidator().Validate(
            new ConfirmarPagamentoCommand(0, Guid.Empty)).IsValid.Should().BeFalse();
        new ObterPagamentoPorOrdemQueryValidator().Validate(
            new ObterPagamentoPorOrdemQuery(0)).IsValid.Should().BeFalse();
    }

    private static Orcamento CriarOrcamentoAprovado()
    {
        var orcamento = CriarOrcamento();
        orcamento.Aprovar(DateTimeOffset.UtcNow);
        return orcamento;
    }

    private static Orcamento CriarOrcamento() => Orcamento.Gerar(
        Guid.NewGuid(), 3007, 1, "Pastilhas desgastadas.",
        [new ItemServicoOrcamento(1000, "Alinhamento", 150m, 30)],
        [new ItemPecaOrcamento(1000, "Pastilha", 2, 45m)],
        DateTimeOffset.UtcNow);

    private static Pagamento CriarPagamento() => Pagamento.Criar(
        Guid.NewGuid(), Guid.NewGuid(), 3007, 219.90m, DateTimeOffset.UtcNow);
}
