using Fiap.TechChallenge.OficinaMecanica.Billing.Domain.Entities;
using FluentAssertions;

namespace Fiap.TechChallenge.OficinaMecanica.Billing.Test.UnitTests.Units.Domain;

public sealed class PagamentoTests
{
    [Fact]
    public void Criar_DeveIniciarPendenteComValorDoOrcamento()
    {
        var correlationId = Guid.NewGuid();
        var orcamentoId = Guid.NewGuid();
        var criadoEm = DateTimeOffset.UtcNow;

        var pagamento = Pagamento.Criar(correlationId, orcamentoId, 3007, 219.90m, criadoEm);

        pagamento.Status.Should().Be("Pendente");
        pagamento.Valor.Should().Be(219.90m);
        pagamento.CorrelationId.Should().Be(correlationId);
        pagamento.OrcamentoId.Should().Be(orcamentoId);
        pagamento.CriadoEm.Should().Be(criadoEm);
    }

    [Fact]
    public void Confirmar_DeveSerIdempotente()
    {
        var pagamento = CriarPagamento();
        var confirmadoEm = DateTimeOffset.UtcNow;

        pagamento.Confirmar(confirmadoEm).Should().BeTrue();
        pagamento.Confirmar(confirmadoEm.AddSeconds(1)).Should().BeFalse();

        pagamento.Status.Should().Be("Confirmado");
        pagamento.ConfirmadoEm.Should().Be(confirmadoEm);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Criar_DeveRejeitarValorNaoPositivo(decimal valor)
    {
        var action = () => Pagamento.Criar(
            Guid.NewGuid(), Guid.NewGuid(), 3007, valor, DateTimeOffset.UtcNow);

        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void RegistrarCobranca_DeveGuardarLinkUmaUnicaVez()
    {
        var pagamento = CriarPagamento();

        pagamento.RegistrarCobranca("pref-1", "https://mp.test/1").Should().BeTrue();
        pagamento.RegistrarCobranca("pref-2", "https://mp.test/2").Should().BeFalse();

        pagamento.PossuiCobranca.Should().BeTrue();
        pagamento.CobrancaId.Should().Be("pref-1");
        pagamento.LinkPagamento.Should().Be("https://mp.test/1");
    }

    [Theory]
    [InlineData("", "https://mp.test/1")]
    [InlineData("pref-1", " ")]
    public void RegistrarCobranca_DeveExigirIdELink(string cobrancaId, string link)
    {
        var action = () => CriarPagamento().RegistrarCobranca(cobrancaId, link);

        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void RegistrarCobranca_DeveFalharForaDePendente()
    {
        var pagamento = CriarPagamento();
        pagamento.Confirmar(DateTimeOffset.UtcNow);

        var action = () => pagamento.RegistrarCobranca("pref-1", "https://mp.test/1");

        action.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Confirmar_DeveGuardarIdDoMercadoPago()
    {
        var pagamento = CriarPagamento();

        pagamento.Confirmar(DateTimeOffset.UtcNow, "123");

        pagamento.MercadoPagoPagamentoId.Should().Be("123");
    }

    [Fact]
    public void Recusar_DeveSerIdempotenteEGuardarMotivo()
    {
        var pagamento = CriarPagamento();
        var recusadoEm = DateTimeOffset.UtcNow;

        pagamento.Recusar(recusadoEm, "rejected: cc_rejected_other_reason", "123").Should().BeTrue();
        pagamento.Recusar(recusadoEm.AddSeconds(1), "outro").Should().BeFalse();

        pagamento.Status.Should().Be(Pagamento.StatusRecusado);
        pagamento.RecusadoEm.Should().Be(recusadoEm);
        pagamento.MotivoRecusa.Should().Be("rejected: cc_rejected_other_reason");
        pagamento.MercadoPagoPagamentoId.Should().Be("123");
    }

    [Fact]
    public void Recusar_DeveExigirMotivo()
    {
        var action = () => CriarPagamento().Recusar(DateTimeOffset.UtcNow, " ");

        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void DesfechosOpostos_DevemSerRejeitados()
    {
        var confirmado = CriarPagamento();
        confirmado.Confirmar(DateTimeOffset.UtcNow);
        var recusado = CriarPagamento();
        recusado.Recusar(DateTimeOffset.UtcNow, "rejected");

        ((Action)(() => confirmado.Recusar(DateTimeOffset.UtcNow, "rejected")))
            .Should().Throw<InvalidOperationException>();
        ((Action)(() => recusado.Confirmar(DateTimeOffset.UtcNow)))
            .Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Criar_DeveGerarIdDeterministicoPorSagaETentativa()
    {
        var correlationId = Guid.NewGuid();

        var primeira = Pagamento.Criar(correlationId, Guid.NewGuid(), 3007, 219.90m, DateTimeOffset.UtcNow);
        var repetida = Pagamento.Criar(correlationId, Guid.NewGuid(), 3007, 219.90m, DateTimeOffset.UtcNow);
        var segunda = Pagamento.Criar(correlationId, Guid.NewGuid(), 3007, 219.90m, DateTimeOffset.UtcNow, 2);

        primeira.Tentativa.Should().Be(1);
        segunda.Tentativa.Should().Be(2);
        repetida.Id.Should().Be(primeira.Id);
        segunda.Id.Should().NotBe(primeira.Id);
        segunda.Id.Should().Be(Pagamento.GerarId(correlationId, 2));
        Pagamento.GerarId(Guid.NewGuid(), 1).Should().NotBe(primeira.Id);
    }

    [Fact]
    public void Criar_DeveRejeitarTentativaInvalida()
    {
        var action = () => Pagamento.Criar(
            Guid.NewGuid(), Guid.NewGuid(), 3007, 219.90m, DateTimeOffset.UtcNow, 0);

        action.Should().Throw<ArgumentException>();
    }

    private static Pagamento CriarPagamento() => Pagamento.Criar(
        Guid.NewGuid(), Guid.NewGuid(), 3007, 219.90m, DateTimeOffset.UtcNow);
}
