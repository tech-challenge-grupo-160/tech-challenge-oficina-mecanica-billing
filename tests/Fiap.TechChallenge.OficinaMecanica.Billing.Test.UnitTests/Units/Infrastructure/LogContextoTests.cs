using Fiap.TechChallenge.OficinaMecanica.Billing.Infrastructure.Logging;
using Fiap.TechChallenge.OficinaMecanica.Billing.Shared.Logging;
using FluentAssertions;
using Microsoft.Extensions.Logging;

namespace Fiap.TechChallenge.OficinaMecanica.Billing.Test.UnitTests.Units.Infrastructure;

public sealed class LogContextoTests
{
    [Fact]
    public void IniciarParaMensagem_DeveLerCorrelationIdOsEMessageId()
    {
        var logger = new LoggerDeEscopo();
        var mensagem = new MensagemTeste(Guid.NewGuid(), Guid.NewGuid(), 3007);

        using (logger.IniciarParaMensagem(mensagem))
        {
            logger.Escopo.Should().Contain(new KeyValuePair<string, object?>(LogContexto.CorrelationId, mensagem.CorrelationId))
                .And.Contain(new KeyValuePair<string, object?>(LogContexto.OrdemServicoId, 3007))
                .And.Contain(new KeyValuePair<string, object?>(LogContexto.MessageId, mensagem.MessageId))
                .And.Contain(new KeyValuePair<string, object?>(LogContexto.TipoMensagem, nameof(MensagemTeste)));
        }
    }

    [Fact]
    public void IniciarParaMensagem_DeveIgnorarPropriedadesAusentesOuDeOutroTipo()
    {
        var logger = new LoggerDeEscopo();

        using (logger.IniciarParaMensagem(new { CorrelationId = "texto", OrdemServicoId = 5L }, "Outro"))
        {
            logger.Escopo.Should().ContainSingle()
                .Which.Should().Be(new KeyValuePair<string, object?>(LogContexto.TipoMensagem, "Outro"));
        }
    }

    [Fact]
    public void Iniciar_SemValoresNaoDeveAbrirEscopo()
    {
        var logger = new LoggerDeEscopo();

        logger.Iniciar(Guid.Empty, 0).Should().BeNull();
        logger.Escopo.Should().BeNull();
    }

    [Theory]
    [InlineData("""{"ordemServicoId":3007}""", 3007)]
    [InlineData("""{"OrdemServicoId":42}""", 42)]
    [InlineData("""{"ordemServicoId":"3007"}""", null)]
    [InlineData("""{"outro":1}""", null)]
    [InlineData("[]", null)]
    [InlineData("nao-json", null)]
    [InlineData("", null)]
    public void OutboxLogContexto_DeveLerNumeroDaOsDoPayload(string payload, int? esperado) =>
        OutboxLogContexto.LerOrdemServicoId(payload).Should().Be(esperado);

    private sealed record MensagemTeste(Guid MessageId, Guid CorrelationId, int OrdemServicoId);

    private sealed class LoggerDeEscopo : ILogger
    {
        public IReadOnlyDictionary<string, object?>? Escopo { get; private set; }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            Escopo = (IReadOnlyDictionary<string, object?>)state;
            return null;
        }

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
        }
    }
}
