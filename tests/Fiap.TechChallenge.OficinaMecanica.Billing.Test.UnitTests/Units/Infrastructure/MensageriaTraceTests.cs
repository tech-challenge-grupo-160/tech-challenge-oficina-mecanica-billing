using System.Text;
using Amazon.SQS.Model;
using Fiap.TechChallenge.OficinaMecanica.Billing.Infrastructure.Observability;
using FluentAssertions;

namespace Fiap.TechChallenge.OficinaMecanica.Billing.Test.UnitTests.Units.Infrastructure;

public sealed class MensageriaTraceTests
{
    private const string ContextoDaSaga =
        """{"x-datadog-trace-id":"111","x-datadog-parent-id":"5","traceparent":"00-000000000000000000000000000006f-0000000000000005-01"}""";
    private const string ContextoDaSagaOutroSpan = """{"x-datadog-trace-id":"111","x-datadog-parent-id":"9"}""";
    private const string ContextoDeRequisicaoHttp = """{"x-datadog-trace-id":"222","x-datadog-parent-id":"7"}""";

    [Fact]
    public void EscolherContextoPai_DeveUsarContextoDaMensagemQuandoServicoNaoConheceASaga()
    {
        var contexto = MensageriaTrace.EscolherContextoPai(ContextoDeRequisicaoHttp, null);

        contexto.Should().Be(new ContextoPublicacao(ContextoDeRequisicaoHttp, null));
    }

    [Fact]
    public void EscolherContextoPai_DeveUsarContextoDaMensagemQuandoJaEstaNoTraceDaSaga()
    {
        var contexto = MensageriaTrace.EscolherContextoPai(ContextoDaSagaOutroSpan, ContextoDaSaga);

        contexto.Should().Be(new ContextoPublicacao(ContextoDaSagaOutroSpan, null));
    }

    [Fact]
    public void EscolherContextoPai_DeveContinuarTraceDaSagaQuandoMensagemVemDeEtapaHumana()
    {
        var contexto = MensageriaTrace.EscolherContextoPai(ContextoDeRequisicaoHttp, ContextoDaSaga);

        contexto.Should().Be(new ContextoPublicacao(ContextoDaSaga, "222"));
    }

    [Fact]
    public void EscolherContextoPai_DeveUsarTraceDaSagaQuandoMensagemNaoTemContexto()
    {
        var contexto = MensageriaTrace.EscolherContextoPai(null, ContextoDaSaga);

        contexto.Should().Be(new ContextoPublicacao(ContextoDaSaga, null));
    }

    [Fact]
    public void EscolherContextoPai_DeveIgnorarContextoDaSagaInvalido()
    {
        var contexto = MensageriaTrace.EscolherContextoPai(ContextoDeRequisicaoHttp, "nao e json");

        contexto.Should().Be(new ContextoPublicacao(ContextoDeRequisicaoHttp, null));
    }

    [Fact]
    public void LerAtributo_DeveLerAtributoString()
    {
        var atributos = new Dictionary<string, MessageAttributeValue>
        {
            ["_datadog"] = new() { DataType = "String", StringValue = ContextoDaSaga }
        };

        MensageriaTrace.LerAtributo(atributos).Should().Be(ContextoDaSaga);
    }

    [Fact]
    public void LerAtributo_DeveLerAtributoBinaryDoSnsComRawDelivery()
    {
        var atributos = new Dictionary<string, MessageAttributeValue>
        {
            ["_datadog"] = new()
            {
                DataType = "Binary",
                BinaryValue = new MemoryStream(Encoding.UTF8.GetBytes(ContextoDaSaga))
            }
        };

        MensageriaTrace.LerAtributo(atributos).Should().Be(ContextoDaSaga);
    }

    [Fact]
    public void LerAtributo_DeveDevolverNuloSemAtributo()
    {
        MensageriaTrace.LerAtributo(null).Should().BeNull();
        MensageriaTrace.LerAtributo(new Dictionary<string, MessageAttributeValue>()).Should().BeNull();
    }

    [Fact]
    public void SemTracerAutomatico_DeveFuncionarSemContexto()
    {
        // Sem o profiler do Datadog (testes, dotnet run local) a API manual nao
        // faz nada: nenhum contexto capturado e os spans nao falham.
        using var publicacao = MensageriaTrace.IniciarPublicacao(
            new ContextoPublicacao(ContextoDaSaga, "222"),
            "GerarOrcamentoMessage",
            MensageriaTrace.SistemaSqs,
            "BillingGerarOrcamento",
            Guid.NewGuid(),
            3007);
        using var consumo = MensageriaTrace.IniciarConsumo(
            ContextoDaSaga,
            "OrcamentoGeradoMessage",
            "https://sqs.us-east-1.amazonaws.com/000000000000/tc-grupo160-dev-os-eventos");
        MensageriaTrace.IdentificarMensagem(consumo, Guid.NewGuid(), 3007);

        MensageriaTrace.CapturarContextoAtual().Should().BeNull();
    }
}
