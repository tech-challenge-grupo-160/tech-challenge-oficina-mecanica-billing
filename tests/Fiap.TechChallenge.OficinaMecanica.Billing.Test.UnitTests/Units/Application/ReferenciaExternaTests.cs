using Fiap.TechChallenge.OficinaMecanica.Billing.Application.Pagamentos;
using FluentAssertions;

namespace Fiap.TechChallenge.OficinaMecanica.Billing.Test.UnitTests.Units.Application;

public sealed class ReferenciaExternaTests
{
    [Fact]
    public void Criar_DeveReferenciarOrdemDeServicoEPagamento()
    {
        var pagamentoId = Guid.NewGuid();

        var referencia = ReferenciaExterna.Criar(3007, pagamentoId);

        referencia.Should().Be($"os:3007;pagamento:{pagamentoId}");
        ReferenciaExterna.TentarLer(referencia, out var ordemServicoId, out var lido).Should().BeTrue();
        ordemServicoId.Should().Be(3007);
        lido.Should().Be(pagamentoId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("os:3007")]
    [InlineData("os:abc;pagamento:00000000-0000-0000-0000-000000000001")]
    [InlineData("os:0;pagamento:00000000-0000-0000-0000-000000000001")]
    [InlineData("os:-1;pagamento:00000000-0000-0000-0000-000000000001")]
    [InlineData("os:3007;pagamento:nao-e-guid")]
    [InlineData("os:3007;pagamento:00000000-0000-0000-0000-000000000000")]
    [InlineData("pedido:3007;pagamento:00000000-0000-0000-0000-000000000001")]
    [InlineData("os:3007;cobranca:00000000-0000-0000-0000-000000000001")]
    public void TentarLer_DeveRejeitarReferenciaInvalida(string? referencia) =>
        ReferenciaExterna.TentarLer(referencia, out _, out _).Should().BeFalse();
}
