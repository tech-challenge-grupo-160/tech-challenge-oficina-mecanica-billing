using Fiap.TechChallenge.OficinaMecanica.Billing.Application.Abstractions;
using Fiap.TechChallenge.OficinaMecanica.Billing.Application.Messaging;
using Fiap.TechChallenge.OficinaMecanica.Billing.Application.Pagamentos;
using Fiap.TechChallenge.OficinaMecanica.Billing.Domain.Entities;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Fiap.TechChallenge.OficinaMecanica.Billing.Test.UnitTests.Units.Application;

public sealed class ProcessarNotificacaoMercadoPagoTests
{
    private readonly Pagamento _pagamento = Pagamento.Criar(
        Guid.NewGuid(), Guid.NewGuid(), 3007, 219.90m, DateTimeOffset.UtcNow);
    private readonly Mock<IMercadoPagoGateway> _gateway = new();
    private readonly Mock<IPagamentoRepository> _pagamentos = new();

    public ProcessarNotificacaoMercadoPagoTests()
    {
        _pagamentos.Setup(repository => repository.ObterAsync(
                3007, _pagamento.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _pagamento);
        _pagamentos.Setup(repository => repository.ConfirmarAsync(
                It.IsAny<Pagamento>(), It.IsAny<PagamentoConfirmadoMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Pagamento pagamento, PagamentoConfirmadoMessage _, CancellationToken _) => (pagamento, true));
        _pagamentos.Setup(repository => repository.RecusarAsync(
                It.IsAny<Pagamento>(), It.IsAny<PagamentoRecusadoMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Pagamento pagamento, PagamentoRecusadoMessage _, CancellationToken _) => (pagamento, true));
    }

    [Fact]
    public async Task Aprovado_DeveConfirmarEPublicarEvento()
    {
        PagamentoConfirmadoMessage? evento = null;
        _pagamentos.Setup(repository => repository.ConfirmarAsync(
                It.IsAny<Pagamento>(), It.IsAny<PagamentoConfirmadoMessage>(), It.IsAny<CancellationToken>()))
            .Callback<Pagamento, PagamentoConfirmadoMessage, CancellationToken>((_, message, _) => evento = message)
            .ReturnsAsync((Pagamento pagamento, PagamentoConfirmadoMessage _, CancellationToken _) => (pagamento, true));
        ConsultaRetorna("approved");

        var result = await Processar();

        result.Desfecho.Should().Be(NotificacaoMercadoPagoResult.Confirmado);
        result.PagamentoId.Should().Be(_pagamento.Id);
        _pagamento.Status.Should().Be(Pagamento.StatusConfirmado);
        _pagamento.MercadoPagoPagamentoId.Should().Be("123");
        evento!.OrdemServicoId.Should().Be(3007);
        evento.Valor.Should().Be(219.90m);
    }

    [Theory]
    [InlineData("cancelled", null, "cancelled")]
    [InlineData("cancelled", "expired", "cancelled: expired")]
    public async Task Recusado_DeveRecusarEPublicarEvento(string status, string? detalhe, string motivo)
    {
        PagamentoRecusadoMessage? evento = null;
        _pagamentos.Setup(repository => repository.RecusarAsync(
                It.IsAny<Pagamento>(), It.IsAny<PagamentoRecusadoMessage>(), It.IsAny<CancellationToken>()))
            .Callback<Pagamento, PagamentoRecusadoMessage, CancellationToken>((_, message, _) => evento = message)
            .ReturnsAsync((Pagamento pagamento, PagamentoRecusadoMessage _, CancellationToken _) => (pagamento, true));
        ConsultaRetorna(status, detalhe: detalhe);

        var result = await Processar();

        result.Desfecho.Should().Be(NotificacaoMercadoPagoResult.Recusado);
        _pagamento.Status.Should().Be(Pagamento.StatusRecusado);
        evento!.Motivo.Should().Be(motivo);
        evento.Status.Should().Be(Pagamento.StatusRecusado);
    }

    [Fact]
    public async Task NotificacaoRepetida_NaoDevePublicarEventoDeNovo()
    {
        ConsultaRetorna("approved");

        var primeira = await Processar();
        var segunda = await Processar();

        primeira.Desfecho.Should().Be(NotificacaoMercadoPagoResult.Confirmado);
        segunda.Desfecho.Should().Be(NotificacaoMercadoPagoResult.Duplicada);
        _pagamentos.Verify(repository => repository.ConfirmarAsync(
            It.IsAny<Pagamento>(), It.IsAny<PagamentoConfirmadoMessage>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task NotificacaoConcorrente_DeveSerDuplicadaQuandoOutraJaGravou()
    {
        _pagamentos.Setup(repository => repository.ConfirmarAsync(
                It.IsAny<Pagamento>(), It.IsAny<PagamentoConfirmadoMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Pagamento pagamento, PagamentoConfirmadoMessage _, CancellationToken _) => (pagamento, false));
        ConsultaRetorna("approved");

        var result = await Processar();

        result.Desfecho.Should().Be(NotificacaoMercadoPagoResult.Duplicada);
    }

    [Fact]
    public async Task TentativaRecusada_DeveManterPendenteSemPublicarEvento()
    {
        // No Checkout Pro o comprador pode tentar de novo depois de um rejected.
        ConsultaRetorna("rejected", detalhe: "cc_rejected_insufficient_amount");

        var result = await Processar();

        result.Desfecho.Should().Be(NotificacaoMercadoPagoResult.Aguardando);
        result.PagamentoId.Should().Be(_pagamento.Id);
        _pagamento.Status.Should().Be(Pagamento.StatusPendente);
        _pagamentos.Verify(repository => repository.RecusarAsync(
            It.IsAny<Pagamento>(), It.IsAny<PagamentoRecusadoMessage>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task AprovacaoDepoisDeTentativaRecusada_DeveConfirmar()
    {
        // Primeira tentativa recusada (pagamento MP 123), segunda aprovada
        // (pagamento MP 124), na mesma preferencia: o cliente pagou e a OS segue.
        ConsultaRetorna("rejected", detalhe: "cc_rejected_insufficient_amount");
        await Processar();

        ConsultaRetorna("approved", mercadoPagoId: "124");
        var result = await Processar("124");

        result.Desfecho.Should().Be(NotificacaoMercadoPagoResult.Confirmado);
        _pagamento.Status.Should().Be(Pagamento.StatusConfirmado);
        _pagamento.MercadoPagoPagamentoId.Should().Be("124");
    }

    [Fact]
    public async Task RecusaRepetida_DeveSerDuplicada()
    {
        ConsultaRetorna("cancelled");

        await Processar();
        var segunda = await Processar();

        segunda.Desfecho.Should().Be(NotificacaoMercadoPagoResult.Duplicada);
        _pagamentos.Verify(repository => repository.RecusarAsync(
            It.IsAny<Pagamento>(), It.IsAny<PagamentoRecusadoMessage>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory]
    [InlineData("pending")]
    [InlineData("in_process")]
    [InlineData("authorized")]
    public async Task StatusIntermediario_DeveAguardar(string status)
    {
        ConsultaRetorna(status);

        var result = await Processar();

        result.Desfecho.Should().Be(NotificacaoMercadoPagoResult.Aguardando);
        _pagamento.Status.Should().Be(Pagamento.StatusPendente);
    }

    [Fact]
    public async Task PagamentoInexistenteNoMercadoPago_DeveSerIgnorado()
    {
        _gateway.Setup(item => item.ObterPagamentoAsync("123", It.IsAny<CancellationToken>()))
            .ReturnsAsync((PagamentoMercadoPago?)null);

        var result = await Processar();

        result.Desfecho.Should().Be(NotificacaoMercadoPagoResult.Ignorada);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("pedido-de-outro-sistema")]
    public async Task ReferenciaInvalida_DeveSerIgnorada(string? referencia)
    {
        _gateway.Setup(item => item.ObterPagamentoAsync("123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagamentoMercadoPago("123", "approved", null, referencia, 219.90m));

        var result = await Processar();

        result.Desfecho.Should().Be(NotificacaoMercadoPagoResult.Ignorada);
    }

    [Fact]
    public async Task PagamentoDesconhecidoNoBilling_DeveSerIgnorado()
    {
        _gateway.Setup(item => item.ObterPagamentoAsync("123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagamentoMercadoPago(
                "123", "approved", null, ReferenciaExterna.Criar(3007, Guid.NewGuid()), 219.90m));

        var result = await Processar();

        result.Desfecho.Should().Be(NotificacaoMercadoPagoResult.Ignorada);
    }

    [Fact]
    public async Task ValorDivergente_NaoDeveConfirmar()
    {
        ConsultaRetorna("approved", valor: 1m);

        var result = await Processar();

        result.Desfecho.Should().Be(NotificacaoMercadoPagoResult.Ignorada);
        _pagamento.Status.Should().Be(Pagamento.StatusPendente);
    }

    [Fact]
    public async Task AprovacaoDepoisDaRecusa_DeveSerIgnorada()
    {
        _pagamento.Recusar(DateTimeOffset.UtcNow, "cancelled");
        ConsultaRetorna("approved");

        var result = await Processar();

        result.Desfecho.Should().Be(NotificacaoMercadoPagoResult.Ignorada);
        _pagamento.Status.Should().Be(Pagamento.StatusRecusado);
    }

    [Fact]
    public async Task RecusaDepoisDaConfirmacao_DeveSerIgnorada()
    {
        _pagamento.Confirmar(DateTimeOffset.UtcNow);
        ConsultaRetorna("cancelled");

        var result = await Processar();

        result.Desfecho.Should().Be(NotificacaoMercadoPagoResult.Ignorada);
        _pagamento.Status.Should().Be(Pagamento.StatusConfirmado);
    }

    [Theory]
    [InlineData("")]
    [InlineData("123;drop")]
    public void Validator_DeveRejeitarIdInvalido(string id) =>
        new ProcessarNotificacaoMercadoPagoCommandValidator()
            .Validate(new ProcessarNotificacaoMercadoPagoCommand(id)).IsValid.Should().BeFalse();

    private void ConsultaRetorna(
        string status,
        decimal valor = 219.90m,
        string? detalhe = null,
        string mercadoPagoId = "123") =>
        _gateway.Setup(item => item.ObterPagamentoAsync(mercadoPagoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagamentoMercadoPago(
                mercadoPagoId,
                status,
                detalhe,
                ReferenciaExterna.Criar(_pagamento.OrdemServicoId, _pagamento.Id),
                valor));

    private Task<NotificacaoMercadoPagoResult> Processar(string mercadoPagoId = "123") =>
        new ProcessarNotificacaoMercadoPagoCommandHandler(
                _gateway.Object,
                _pagamentos.Object,
                NullLogger<ProcessarNotificacaoMercadoPagoCommandHandler>.Instance)
            .Handle(new ProcessarNotificacaoMercadoPagoCommand(mercadoPagoId), CancellationToken.None);
}
