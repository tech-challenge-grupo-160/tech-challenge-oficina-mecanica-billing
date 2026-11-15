using Fiap.TechChallenge.OficinaMecanica.Billing.Application.Abstractions;
using Fiap.TechChallenge.OficinaMecanica.Billing.Application.Exceptions;
using Fiap.TechChallenge.OficinaMecanica.Billing.Application.Messaging;
using Fiap.TechChallenge.OficinaMecanica.Billing.Application.Orcamentos;
using Fiap.TechChallenge.OficinaMecanica.Billing.Domain.Entities;
using FluentAssertions;
using Moq;

namespace Fiap.TechChallenge.OficinaMecanica.Billing.Test.UnitTests.Units.Application;

public class OrcamentoHandlersTests
{
    [Fact]
    public async Task Gerar_DevePersistirOrcamentoEEventoComTotais()
    {
        var repository = new Mock<IOrcamentoRepository>();
        Orcamento? recebido = null;
        OrcamentoGeradoMessage? evento = null;
        repository.Setup(item => item.CriarOuObterAsync(
                It.IsAny<Orcamento>(), It.IsAny<Guid>(), It.IsAny<OrcamentoGeradoMessage>(),
                It.IsAny<CancellationToken>()))
            .Callback<Orcamento, Guid, OrcamentoGeradoMessage, CancellationToken>(
                (orcamento, _, mensagem, _) =>
                {
                    recebido = orcamento;
                    evento = mensagem;
                })
            .ReturnsAsync(() => (recebido!, true));
        var handler = new GerarOrcamentoCommandHandler(repository.Object);
        var command = CriarCommand();

        var result = await handler.Handle(command, CancellationToken.None);

        result.Criado.Should().BeTrue();
        result.ValorTotal.Should().Be(240m);
        recebido.Should().NotBeNull();
        evento.Should().NotBeNull();
        evento!.OrcamentoId.Should().Be(recebido!.Id);
        evento.CorrelationId.Should().Be(command.CorrelationId);
        evento.OrdemServicoId.Should().Be(3007);
        evento.ValorTotal.Should().Be(240m);
        evento.Versao.Should().Be(1);
    }

    [Fact]
    public async Task Gerar_DeveRetornarExistenteQuandoMensagemForReprocessada()
    {
        var command = CriarCommand();
        var existente = CriarOrcamento(command.CorrelationId, command.DiagnosticoId);
        var repository = new Mock<IOrcamentoRepository>();
        repository.Setup(item => item.CriarOuObterAsync(
                It.IsAny<Orcamento>(), command.MensagemEntradaId,
                It.IsAny<OrcamentoGeradoMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((existente, false));

        var result = await new GerarOrcamentoCommandHandler(repository.Object)
            .Handle(command, CancellationToken.None);

        result.Id.Should().Be(existente.Id);
        result.Criado.Should().BeFalse();
    }

    [Fact]
    public async Task Gerar_DeveFalharQuandoExistenteForDeOutroDiagnostico()
    {
        var command = CriarCommand();
        var repository = new Mock<IOrcamentoRepository>();
        repository.Setup(item => item.CriarOuObterAsync(
                It.IsAny<Orcamento>(), It.IsAny<Guid>(),
                It.IsAny<OrcamentoGeradoMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CriarOrcamento(Guid.NewGuid(), 99), false));

        var action = () => new GerarOrcamentoCommandHandler(repository.Object)
            .Handle(command, CancellationToken.None);

        await action.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Obter_DeveRetornarOrcamentoExistente()
    {
        var orcamento = CriarOrcamento(Guid.NewGuid(), 1);
        var repository = new Mock<IOrcamentoRepository>();
        repository.Setup(item => item.ObterPorOrdemServicoIdAsync(
                3007, It.IsAny<CancellationToken>()))
            .ReturnsAsync(orcamento);

        var result = await new ObterOrcamentoPorOrdemQueryHandler(repository.Object)
            .Handle(new ObterOrcamentoPorOrdemQuery(3007), CancellationToken.None);

        result.Id.Should().Be(orcamento.Id);
        result.ValorTotal.Should().Be(240m);
    }

    [Fact]
    public async Task Obter_DeveRetornarNotFoundQuandoNaoExistir()
    {
        var repository = new Mock<IOrcamentoRepository>();
        repository.Setup(item => item.ObterPorOrdemServicoIdAsync(
                3007, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Orcamento?)null);

        var action = () => new ObterOrcamentoPorOrdemQueryHandler(repository.Object)
            .Handle(new ObterOrcamentoPorOrdemQuery(3007), CancellationToken.None);

        await action.Should().ThrowAsync<ServiceNotFoundException>();
    }

    [Fact]
    public void Validators_DeveAceitarComandoValidoERejeitarConsultasInvalidas()
    {
        new GerarOrcamentoCommandValidator().Validate(CriarCommand()).IsValid.Should().BeTrue();
        new GerarOrcamentoCommandValidator().Validate(CriarCommand() with
        {
            MensagemEntradaId = Guid.Empty,
            CorrelationId = Guid.Empty,
            ParecerTecnico = "",
            Servicos = []
        }).IsValid.Should().BeFalse();
        new ObterOrcamentoPorOrdemQueryValidator()
            .Validate(new ObterOrcamentoPorOrdemQuery(0)).IsValid.Should().BeFalse();
    }

    [Fact]
    public void ResponderValidator_DeveExigirMotivoSomenteNaRecusa()
    {
        var validator = new ResponderOrcamentoCommandValidator();
        var orcamentoId = Guid.NewGuid();

        validator.Validate(new ResponderOrcamentoCommand(3007, orcamentoId, true, null))
            .IsValid.Should().BeTrue();
        validator.Validate(new ResponderOrcamentoCommand(3007, orcamentoId, false, null))
            .IsValid.Should().BeFalse();
        validator.Validate(new ResponderOrcamentoCommand(
                0, Guid.Empty, false, new string('x', 501)))
            .IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData(true, "Aprovado", nameof(OrcamentoAprovadoMessage))]
    [InlineData(false, "Recusado", nameof(OrcamentoRecusadoMessage))]
    public async Task Responder_DevePersistirDecisaoEPublicarEvento(
        bool aprovado,
        string status,
        string tipoEvento)
    {
        var orcamento = CriarOrcamento(Guid.NewGuid(), 1);
        var repository = new Mock<IOrcamentoRepository>();
        repository.Setup(item => item.ObterPorOrdemServicoIdAsync(
                3007, It.IsAny<CancellationToken>()))
            .ReturnsAsync(orcamento);
        repository.Setup(item => item.RegistrarDecisaoAsync(
                orcamento, It.IsAny<Guid>(), tipoEvento, It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((orcamento, true));

        var result = await new ResponderOrcamentoCommandHandler(repository.Object)
            .Handle(new ResponderOrcamentoCommand(
                3007, orcamento.Id, aprovado,
                aprovado ? null : "Valor nao aprovado."), CancellationToken.None);

        result.Status.Should().Be(status);
        result.RespondidoEm.Should().NotBeNull();
        repository.VerifyAll();
    }

    [Fact]
    public async Task Responder_DeveOcultarOrcamentoComIdentificadorIncorreto()
    {
        var orcamento = CriarOrcamento(Guid.NewGuid(), 1);
        var repository = new Mock<IOrcamentoRepository>();
        repository.Setup(item => item.ObterPorOrdemServicoIdAsync(
                3007, It.IsAny<CancellationToken>()))
            .ReturnsAsync(orcamento);

        var action = () => new ResponderOrcamentoCommandHandler(repository.Object)
            .Handle(new ResponderOrcamentoCommand(
                3007, Guid.NewGuid(), true, null), CancellationToken.None);

        await action.Should().ThrowAsync<ServiceNotFoundException>();
    }

    private static GerarOrcamentoCommand CriarCommand() => new(
        Guid.NewGuid(), Guid.NewGuid(), 3007, 1, "Pastilhas desgastadas.",
        [new ServicoOrcamentoCommand(1000, "Alinhamento", 150m, 30)],
        [new PecaOrcamentoCommand(1000, "Pastilha de Freio", 2, 45m)],
        DateTimeOffset.UtcNow);

    private static Orcamento CriarOrcamento(Guid correlationId, int diagnosticoId) =>
        Orcamento.Gerar(
            correlationId, 3007, diagnosticoId, "Pastilhas desgastadas.",
            [new ItemServicoOrcamento(1000, "Alinhamento", 150m, 30)],
            [new ItemPecaOrcamento(1000, "Pastilha de Freio", 2, 45m)],
            DateTimeOffset.UtcNow);
}
