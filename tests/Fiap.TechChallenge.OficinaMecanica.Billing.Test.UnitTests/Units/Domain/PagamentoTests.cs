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

    private static Pagamento CriarPagamento() => Pagamento.Criar(
        Guid.NewGuid(), Guid.NewGuid(), 3007, 219.90m, DateTimeOffset.UtcNow);
}
