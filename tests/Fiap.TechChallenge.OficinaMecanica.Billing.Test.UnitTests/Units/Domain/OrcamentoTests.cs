using Fiap.TechChallenge.OficinaMecanica.Billing.Domain.Entities;
using FluentAssertions;

namespace Fiap.TechChallenge.OficinaMecanica.Billing.Test.UnitTests.Units.Domain;

public class OrcamentoTests
{
    [Fact]
    public void Gerar_DeveCriarOrcamentoComSnapshotsETotais()
    {
        var correlationId = Guid.NewGuid();
        var criadoEm = DateTimeOffset.UtcNow;

        var orcamento = Orcamento.Gerar(
            correlationId,
            3007,
            1,
            "  Pastilhas desgastadas.  ",
            [new ItemServicoOrcamento(1000, "Alinhamento", 150m, 30)],
            [new ItemPecaOrcamento(1000, "Pastilha de Freio", 2, 45m)],
            criadoEm);

        orcamento.Id.Should().NotBeEmpty();
        orcamento.CorrelationId.Should().Be(correlationId);
        orcamento.OrdemServicoId.Should().Be(3007);
        orcamento.DiagnosticoId.Should().Be(1);
        orcamento.ParecerTecnico.Should().Be("Pastilhas desgastadas.");
        orcamento.Status.Should().Be("Gerado");
        orcamento.ValorServicos.Should().Be(150m);
        orcamento.ValorPecas.Should().Be(90m);
        orcamento.ValorTotal.Should().Be(240m);
        orcamento.CriadoEm.Should().Be(criadoEm);
        orcamento.CorrespondeAoDiagnostico(correlationId, 1).Should().BeTrue();
        orcamento.CorrespondeAoDiagnostico(Guid.NewGuid(), 1).Should().BeFalse();
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    public void Gerar_DeveRejeitarIdentificadoresInvalidos(int ordemServicoId, int diagnosticoId)
    {
        var action = () => Orcamento.Gerar(
            Guid.NewGuid(),
            ordemServicoId,
            diagnosticoId,
            "Parecer",
            [new ItemServicoOrcamento(1, "Servico", 10m, 10)],
            [],
            DateTimeOffset.UtcNow);

        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Gerar_DeveRejeitarCorrelationIdVazio()
    {
        var action = () => CriarValido(Guid.Empty);

        action.Should().Throw<ArgumentException>().WithParameterName("correlationId");
    }

    [Fact]
    public void Gerar_DeveRejeitarParecerVazio()
    {
        var action = () => Orcamento.Gerar(
            Guid.NewGuid(), 1, 1, " ",
            [new ItemServicoOrcamento(1, "Servico", 10m, 10)], [], DateTimeOffset.UtcNow);

        action.Should().Throw<ArgumentException>().WithParameterName("parecerTecnico");
    }

    [Fact]
    public void Gerar_DeveRejeitarServicosAusentesOuDuplicados()
    {
        var semServico = () => Orcamento.Gerar(
            Guid.NewGuid(), 1, 1, "Parecer", [], [], DateTimeOffset.UtcNow);
        var duplicado = () => Orcamento.Gerar(
            Guid.NewGuid(), 1, 1, "Parecer",
            [
                new ItemServicoOrcamento(1, "Servico", 10m, 10),
                new ItemServicoOrcamento(1, "Servico", 10m, 10)
            ], [], DateTimeOffset.UtcNow);

        semServico.Should().Throw<ArgumentException>();
        duplicado.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Gerar_DeveRejeitarPecasDuplicadas()
    {
        var action = () => Orcamento.Gerar(
            Guid.NewGuid(), 1, 1, "Parecer",
            [new ItemServicoOrcamento(1, "Servico", 10m, 10)],
            [
                new ItemPecaOrcamento(1, "Peca", 1, 5m),
                new ItemPecaOrcamento(1, "Peca", 1, 5m)
            ], DateTimeOffset.UtcNow);

        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Itens_DeveValidarDadosTecnicosEComerciais()
    {
        new ItemServicoOrcamento(1, "Servico", 0m, 1).Validar()
            .Should().NotBeNull();
        new ItemPecaOrcamento(1, "Peca", 1, 0m).Validar()
            .Should().NotBeNull();

        var servicoInvalido = () => new ItemServicoOrcamento(0, "", -1m, 0).Validar();
        var pecaInvalida = () => new ItemPecaOrcamento(0, "", 0, -1m).Validar();

        servicoInvalido.Should().Throw<ArgumentException>();
        pecaInvalida.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Restaurar_DeveReconstituirOrcamentoPersistido()
    {
        var id = Guid.NewGuid();
        var correlationId = Guid.NewGuid();
        var criadoEm = DateTimeOffset.UtcNow;

        var orcamento = Orcamento.Restaurar(
            id, correlationId, 12, 34, "Parecer", "Gerado", criadoEm,
            [new ItemServicoOrcamento(1, "Servico", 20m, 15)],
            [new ItemPecaOrcamento(2, "Peca", 3, 5m)]);

        orcamento.Id.Should().Be(id);
        orcamento.CorrelationId.Should().Be(correlationId);
        orcamento.ValorTotal.Should().Be(35m);
    }

    [Fact]
    public void Aprovar_DeveSerIdempotenteEBloquearDecisaoContraria()
    {
        var orcamento = CriarValido(Guid.NewGuid());
        var respondidoEm = DateTimeOffset.UtcNow;

        orcamento.Aprovar(respondidoEm).Should().BeTrue();
        orcamento.Aprovar(respondidoEm.AddMinutes(1)).Should().BeFalse();
        orcamento.Status.Should().Be("Aprovado");
        orcamento.RespondidoEm.Should().Be(respondidoEm);
        var recusar = () => orcamento.Recusar("Mudei de ideia", respondidoEm);
        recusar.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Recusar_DeveExigirMotivoESerIdempotente()
    {
        var semMotivo = CriarValido(Guid.NewGuid());
        var action = () => semMotivo.Recusar(" ", DateTimeOffset.UtcNow);
        action.Should().Throw<ArgumentException>().WithParameterName("motivo");

        var orcamento = CriarValido(Guid.NewGuid());
        var respondidoEm = DateTimeOffset.UtcNow;
        orcamento.Recusar("  Valor nao aprovado.  ", respondidoEm).Should().BeTrue();
        orcamento.Recusar("Valor nao aprovado.", respondidoEm).Should().BeFalse();
        orcamento.Status.Should().Be("Recusado");
        orcamento.MotivoRecusa.Should().Be("Valor nao aprovado.");
    }

    private static Orcamento CriarValido(Guid correlationId) => Orcamento.Gerar(
        correlationId,
        1,
        1,
        "Parecer",
        [new ItemServicoOrcamento(1, "Servico", 10m, 10)],
        [],
        DateTimeOffset.UtcNow);
}
