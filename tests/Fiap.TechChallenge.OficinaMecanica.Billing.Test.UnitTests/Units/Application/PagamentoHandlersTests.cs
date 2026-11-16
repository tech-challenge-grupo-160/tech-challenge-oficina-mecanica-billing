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

        var result = await new SolicitarPagamentoCommandHandler(orcamentos.Object, pagamentos.Object, Desabilitado())
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
                orcamentos.Object, Mock.Of<IPagamentoRepository>(), Desabilitado())
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

    [Fact]
    public async Task Solicitar_DeveCriarCobrancaNoMercadoPagoQuandoHabilitado()
    {
        var orcamento = CriarOrcamentoAprovado();
        var orcamentos = new Mock<IOrcamentoRepository>();
        orcamentos.Setup(repository => repository.ObterPorOrdemServicoIdAsync(
                3007, It.IsAny<CancellationToken>()))
            .ReturnsAsync(orcamento);
        var pagamentos = new Mock<IPagamentoRepository>();
        pagamentos.Setup(repository => repository.CriarOuObterAsync(
                It.IsAny<Pagamento>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Pagamento candidato, Guid _, CancellationToken _) => (candidato, true));
        pagamentos.Setup(repository => repository.RegistrarCobrancaAsync(
                It.IsAny<Pagamento>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Pagamento pagamento, CancellationToken _) => pagamento);
        var gateway = new Mock<IMercadoPagoGateway>();
        gateway.SetupGet(item => item.Habilitado).Returns(true);
        gateway.Setup(item => item.CriarCobrancaAsync(It.IsAny<Pagamento>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CobrancaMercadoPago("pref-1", "https://mp.test/checkout/pref-1"));

        var result = await new SolicitarPagamentoCommandHandler(orcamentos.Object, pagamentos.Object, gateway.Object)
            .Handle(new SolicitarPagamentoCommand(
                Guid.NewGuid(), orcamento.CorrelationId, orcamento.Id, 3007, DateTimeOffset.UtcNow),
                CancellationToken.None);

        result.LinkPagamento.Should().Be("https://mp.test/checkout/pref-1");
        pagamentos.Verify(repository => repository.RegistrarCobrancaAsync(
            It.Is<Pagamento>(pagamento => pagamento.CobrancaId == "pref-1"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Solicitar_NaoDeveCriarSegundaCobrancaNoReprocessamento()
    {
        var orcamento = CriarOrcamentoAprovado();
        var existente = Pagamento.Criar(
            orcamento.CorrelationId, orcamento.Id, 3007, orcamento.ValorTotal, DateTimeOffset.UtcNow);
        existente.RegistrarCobranca("pref-1", "https://mp.test/checkout/pref-1");
        var orcamentos = new Mock<IOrcamentoRepository>();
        orcamentos.Setup(repository => repository.ObterPorOrdemServicoIdAsync(
                3007, It.IsAny<CancellationToken>()))
            .ReturnsAsync(orcamento);
        var pagamentos = new Mock<IPagamentoRepository>();
        pagamentos.Setup(repository => repository.CriarOuObterAsync(
                It.IsAny<Pagamento>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((existente, false));
        var gateway = new Mock<IMercadoPagoGateway>();
        gateway.SetupGet(item => item.Habilitado).Returns(true);

        var result = await new SolicitarPagamentoCommandHandler(orcamentos.Object, pagamentos.Object, gateway.Object)
            .Handle(new SolicitarPagamentoCommand(
                Guid.NewGuid(), orcamento.CorrelationId, orcamento.Id, 3007, DateTimeOffset.UtcNow),
                CancellationToken.None);

        result.LinkPagamento.Should().Be("https://mp.test/checkout/pref-1");
        gateway.Verify(item => item.CriarCobrancaAsync(
            It.IsAny<Pagamento>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Solicitar_NaoDeveChamarMercadoPagoQuandoDesabilitado()
    {
        var orcamento = CriarOrcamentoAprovado();
        var orcamentos = new Mock<IOrcamentoRepository>();
        orcamentos.Setup(repository => repository.ObterPorOrdemServicoIdAsync(
                3007, It.IsAny<CancellationToken>()))
            .ReturnsAsync(orcamento);
        var pagamentos = new Mock<IPagamentoRepository>();
        pagamentos.Setup(repository => repository.CriarOuObterAsync(
                It.IsAny<Pagamento>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Pagamento candidato, Guid _, CancellationToken _) => (candidato, true));

        var result = await new SolicitarPagamentoCommandHandler(orcamentos.Object, pagamentos.Object, Desabilitado())
            .Handle(new SolicitarPagamentoCommand(
                Guid.NewGuid(), orcamento.CorrelationId, orcamento.Id, 3007, DateTimeOffset.UtcNow),
                CancellationToken.None);

        result.LinkPagamento.Should().BeNull();
        pagamentos.Verify(repository => repository.RegistrarCobrancaAsync(
            It.IsAny<Pagamento>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Solicitar_NovaTentativa_DeveCriarSegundoPagamentoAposRecusa()
    {
        var orcamento = CriarOrcamentoAprovado();
        var anterior = Pagamento.Criar(
            orcamento.CorrelationId, orcamento.Id, 3007, orcamento.ValorTotal, DateTimeOffset.UtcNow);
        anterior.Recusar(DateTimeOffset.UtcNow, "cancelled: expired");
        var orcamentos = new Mock<IOrcamentoRepository>();
        orcamentos.Setup(repository => repository.ObterPorOrdemServicoIdAsync(
                3007, It.IsAny<CancellationToken>()))
            .ReturnsAsync(orcamento);
        var pagamentos = new Mock<IPagamentoRepository>();
        pagamentos.Setup(repository => repository.ObterAsync(3007, anterior.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(anterior);
        pagamentos.Setup(repository => repository.CriarOuObterAsync(
                It.IsAny<Pagamento>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Pagamento candidato, Guid _, CancellationToken _) => (candidato, true));

        var result = await new SolicitarPagamentoCommandHandler(orcamentos.Object, pagamentos.Object, Desabilitado())
            .Handle(new SolicitarPagamentoCommand(
                Guid.NewGuid(), orcamento.CorrelationId, orcamento.Id, 3007, DateTimeOffset.UtcNow, 2),
                CancellationToken.None);

        result.Status.Should().Be("Pendente");
        result.Tentativa.Should().Be(2);
        result.Id.Should().Be(Pagamento.GerarId(orcamento.CorrelationId, 2));
        result.Id.Should().NotBe(anterior.Id);
    }

    [Fact]
    public async Task Solicitar_NovaTentativa_DeveRejeitarQuandoAnteriorNaoFoiRecusada()
    {
        var orcamento = CriarOrcamentoAprovado();
        var anterior = Pagamento.Criar(
            orcamento.CorrelationId, orcamento.Id, 3007, orcamento.ValorTotal, DateTimeOffset.UtcNow);
        var orcamentos = new Mock<IOrcamentoRepository>();
        orcamentos.Setup(repository => repository.ObterPorOrdemServicoIdAsync(
                3007, It.IsAny<CancellationToken>()))
            .ReturnsAsync(orcamento);
        var pagamentos = new Mock<IPagamentoRepository>();
        pagamentos.Setup(repository => repository.ObterAsync(3007, anterior.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(anterior);

        var action = () => new SolicitarPagamentoCommandHandler(orcamentos.Object, pagamentos.Object, Desabilitado())
            .Handle(new SolicitarPagamentoCommand(
                Guid.NewGuid(), orcamento.CorrelationId, orcamento.Id, 3007, DateTimeOffset.UtcNow, 2),
                CancellationToken.None);

        await action.Should().ThrowAsync<InvalidOperationException>();
        pagamentos.Verify(repository => repository.CriarOuObterAsync(
            It.IsAny<Pagamento>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Recusar_DevePersistirEventoComMotivoETentativa()
    {
        var pagamento = CriarPagamento();
        PagamentoRecusadoMessage? evento = null;
        var repository = new Mock<IPagamentoRepository>();
        repository.Setup(item => item.ObterAsync(3007, pagamento.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagamento);
        repository.Setup(item => item.RecusarAsync(
                pagamento, It.IsAny<PagamentoRecusadoMessage>(), It.IsAny<CancellationToken>()))
            .Callback<Pagamento, PagamentoRecusadoMessage, CancellationToken>(
                (_, message, _) => evento = message)
            .ReturnsAsync(() => (pagamento, true));

        var result = await new RecusarPagamentoCommandHandler(repository.Object)
            .Handle(new RecusarPagamentoCommand(3007, pagamento.Id, "simulado"), CancellationToken.None);

        result.Status.Should().Be("Recusado");
        result.MotivoRecusa.Should().Be("simulado");
        evento!.Motivo.Should().Be("simulado");
        evento.Tentativa.Should().Be(1);
        evento.RecusadoEm.Should().Be(pagamento.RecusadoEm);
    }

    [Fact]
    public async Task Recusar_DeveSerIdempotente()
    {
        var pagamento = CriarPagamento();
        pagamento.Recusar(DateTimeOffset.UtcNow, "primeira");
        var repository = new Mock<IPagamentoRepository>();
        repository.Setup(item => item.ObterAsync(3007, pagamento.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagamento);

        var result = await new RecusarPagamentoCommandHandler(repository.Object)
            .Handle(new RecusarPagamentoCommand(3007, pagamento.Id, "segunda"), CancellationToken.None);

        result.MotivoRecusa.Should().Be("primeira");
        repository.Verify(item => item.RecusarAsync(
            It.IsAny<Pagamento>(), It.IsAny<PagamentoRecusadoMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Recusar_DeveRetornarNotFoundQuandoPagamentoNaoExistir()
    {
        var action = () => new RecusarPagamentoCommandHandler(Mock.Of<IPagamentoRepository>())
            .Handle(new RecusarPagamentoCommand(3007, Guid.NewGuid(), "simulado"), CancellationToken.None);

        await action.Should().ThrowAsync<ServiceNotFoundException>();
    }

    [Fact]
    public void Validators_DevemRejeitarRecusaETentativaInvalidas()
    {
        new RecusarPagamentoCommandValidator().Validate(
            new RecusarPagamentoCommand(0, Guid.Empty, "")).IsValid.Should().BeFalse();
        new SolicitarPagamentoCommandValidator().Validate(new SolicitarPagamentoCommand(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 3007, DateTimeOffset.UtcNow, 0))
            .IsValid.Should().BeFalse();
    }

    private static IMercadoPagoGateway Desabilitado() =>
        Mock.Of<IMercadoPagoGateway>(gateway => gateway.Habilitado == false);

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
