using Fiap.TechChallenge.OficinaMecanica.Billing.Application.Messaging;
using FluentAssertions;

namespace Fiap.TechChallenge.OficinaMecanica.Billing.Test.UnitTests.Units.Application;

public class BillingMessagesTests
{
    [Fact]
    public void GerarOrcamento_DevePreservarContratoRecebidoDoOrquestrador()
    {
        var messageId = Guid.NewGuid();
        var correlationId = Guid.NewGuid();
        var ocorridoEm = DateTimeOffset.UtcNow;
        var concluidoEm = ocorridoEm.AddMinutes(-1);

        var message = new GerarOrcamentoMessage(
            messageId,
            correlationId,
            ocorridoEm,
            3007,
            1,
            "Pastilhas desgastadas.",
            [new ServicoGerarOrcamentoMessage(1000, "Alinhamento", 150m, 30)],
            [new PecaGerarOrcamentoMessage(1000, "Pastilha de Freio", 2, 45m)],
            concluidoEm);

        message.MessageId.Should().Be(messageId);
        message.CorrelationId.Should().Be(correlationId);
        message.OcorridoEm.Should().Be(ocorridoEm);
        message.OrdemServicoId.Should().Be(3007);
        message.DiagnosticoId.Should().Be(1);
        message.ParecerTecnico.Should().Be("Pastilhas desgastadas.");
        message.Servicos.Single().Should().Be(
            new ServicoGerarOrcamentoMessage(1000, "Alinhamento", 150m, 30));
        message.Pecas.Single().Should().Be(
            new PecaGerarOrcamentoMessage(1000, "Pastilha de Freio", 2, 45m));
        message.DiagnosticoConcluidoEm.Should().Be(concluidoEm);
        message.Versao.Should().Be(1);
    }

    [Fact]
    public void OrcamentoGerado_DevePreservarContratoPublicadoPeloBilling()
    {
        var messageId = Guid.NewGuid();
        var correlationId = Guid.NewGuid();
        var orcamentoId = Guid.NewGuid();
        var ocorridoEm = DateTimeOffset.UtcNow;

        var message = new OrcamentoGeradoMessage(
            messageId,
            correlationId,
            ocorridoEm,
            orcamentoId,
            3007,
            1,
            "Gerado",
            150m,
            90m,
            240m);

        message.MessageId.Should().Be(messageId);
        message.CorrelationId.Should().Be(correlationId);
        message.OcorridoEm.Should().Be(ocorridoEm);
        message.OrcamentoId.Should().Be(orcamentoId);
        message.OrdemServicoId.Should().Be(3007);
        message.DiagnosticoId.Should().Be(1);
        message.Status.Should().Be("Gerado");
        message.ValorServicos.Should().Be(150m);
        message.ValorPecas.Should().Be(90m);
        message.ValorTotal.Should().Be(240m);
        message.Versao.Should().Be(1);
    }
}
