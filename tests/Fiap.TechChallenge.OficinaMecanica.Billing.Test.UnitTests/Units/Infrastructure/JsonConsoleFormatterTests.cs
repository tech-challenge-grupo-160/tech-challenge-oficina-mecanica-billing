using System.Diagnostics;
using System.Text.Json;
using Fiap.TechChallenge.OficinaMecanica.Billing.Infrastructure.Logging;
using Fiap.TechChallenge.OficinaMecanica.Billing.Shared.Logging;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Fiap.TechChallenge.OficinaMecanica.Billing.Test.UnitTests.Units.Infrastructure;

public sealed class JsonConsoleFormatterTests
{
    private readonly LoggerExternalScopeProvider _scopes = new();

    [Fact]
    public void Write_DeveIncluirCorrelationIdEOrdemServicoDoEscopo()
    {
        var correlationId = Guid.NewGuid();
        using var escopo = _scopes.Push(new Dictionary<string, object?>
        {
            [LogContexto.CorrelationId] = correlationId,
            [LogContexto.OrdemServicoId] = 3007,
            [LogContexto.TipoMensagem] = "GerarOrcamentoMessage"
        });

        var log = Escrever("Comando processado.");

        log.GetProperty("correlationId").GetGuid().Should().Be(correlationId);
        log.GetProperty("ordemServicoId").GetInt32().Should().Be(3007);
        log.GetProperty("tipoMensagem").GetString().Should().Be("GerarOrcamentoMessage");
        log.GetProperty("servico").GetString().Should().Be("oficina-mecanica-billing");
    }

    [Fact]
    public void Write_DeveUsarIdsDoTracerDoDatadogQuandoInjetados()
    {
        using var escopo = _scopes.Push(new Dictionary<string, object?>
        {
            ["dd_trace_id"] = "1234567890123456789",
            ["dd_span_id"] = "987654321",
            ["dd_service"] = "oficina-mecanica-billing",
            ["dd_env"] = "dev",
            ["dd_version"] = "abc123"
        });

        var log = Escrever("mensagem");

        log.GetProperty("trace_id").GetString().Should().Be("1234567890123456789");
        log.GetProperty("span_id").GetString().Should().Be("987654321");
        var dd = log.GetProperty("dd");
        dd.GetProperty("trace_id").GetString().Should().Be("1234567890123456789");
        dd.GetProperty("span_id").GetString().Should().Be("987654321");
        dd.GetProperty("env").GetString().Should().Be("dev");
        dd.GetProperty("version").GetString().Should().Be("abc123");
        log.TryGetProperty("dd_trace_id", out _).Should().BeFalse();
    }

    [Fact]
    public void Write_DeveUsarActivityCorrenteSemTracer()
    {
        using var activity = new Activity("teste").SetIdFormat(ActivityIdFormat.W3C).Start();

        var log = Escrever("mensagem");

        var traceId = activity.TraceId.ToHexString();
        var spanId = activity.SpanId.ToHexString();
        log.GetProperty("trace_id").GetString().Should().Be(traceId);
        log.GetProperty("span_id").GetString().Should().Be(spanId);
        log.GetProperty("dd").GetProperty("trace_id").GetString()
            .Should().Be(Convert.ToUInt64(traceId[16..], 16).ToString());
        log.GetProperty("dd").GetProperty("span_id").GetString()
            .Should().Be(Convert.ToUInt64(spanId, 16).ToString());
    }

    [Fact]
    public void Write_SemTraceNaoDeveGerarBlocoDd()
    {
        Activity.Current = null;

        var log = Escrever("mensagem");

        log.GetProperty("trace_id").ValueKind.Should().Be(JsonValueKind.Null);
        log.TryGetProperty("dd", out _).Should().BeFalse();
    }

    [Fact]
    public void Write_DeveMascararCpfNaMensagemENoEscopo()
    {
        using var escopo = _scopes.Push(new Dictionary<string, object?>
        {
            [LogContexto.TipoMensagem] = "cliente 123.456.789-09"
        });

        var log = Escrever("Cliente {Documento} autenticado", "12345678909");

        log.GetProperty("mensagem").GetString().Should().Be("Cliente ***.***.***-** autenticado");
        log.GetProperty("Documento").GetString().Should().Be("***.***.***-**");
        log.GetProperty("tipoMensagem").GetString().Should().Be("cliente ***.***.***-**");
        log.ToString().Should().NotContain("12345678909").And.NotContain("123.456.789-09");
    }

    [Fact]
    public void Write_DeveIgnorarEscoposForaDoContexto()
    {
        using var hosting = _scopes.Push(new Dictionary<string, object?>
        {
            ["RequestPath"] = "/api/v1/pagamentos",
            ["ConnectionId"] = "abc"
        });
        using var texto = _scopes.Push("escopo em texto");

        var log = Escrever("mensagem");

        log.TryGetProperty("RequestPath", out _).Should().BeFalse();
        log.TryGetProperty("ConnectionId", out _).Should().BeFalse();
    }

    [Fact]
    public void Write_DevePreferirValorDoProprioLog()
    {
        using var escopo = _scopes.Push(new Dictionary<string, object?> { [LogContexto.OrdemServicoId] = 1 });

        var log = Escrever("OS {OrdemServicoId} atualizada", 3007);

        log.GetProperty("ordemServicoId").GetInt32().Should().Be(3007);
        log.TryGetProperty("OrdemServicoId", out _).Should().BeFalse();
    }

    [Fact]
    public void Write_DeveRegistrarExcecaoMascarada()
    {
        var log = Escrever("falha", excecao: new InvalidOperationException("token=abc123"));

        log.GetProperty("nivel").GetString().Should().Be("information");
        log.GetProperty("erro").GetString().Should().Contain("token=[REDACTED]");
    }

    [Fact]
    public void Write_DeveOcultarObjetosComplexos()
    {
        var log = Escrever("payload {Payload}", new { Cpf = "12345678909" });

        log.GetProperty("Payload").GetString().Should().Be("[REDACTED]");
    }

    [Fact]
    public void Write_DeveUsarAtributosPadraoDoDatadog()
    {
        var log = Escrever("Pagamento {statusHttp} recebido", 200);

        log.GetProperty("level").GetString().Should().Be("information");
        log.GetProperty("message").GetString().Should().Be("Pagamento 200 recebido");
        log.GetProperty("statusHttp").GetInt32().Should().Be(200);
        log.TryGetProperty("status", out _).Should().BeFalse();
    }

    private JsonElement Escrever(string template, object? argumento = null, Exception? excecao = null)
    {
        var options = new JsonConsoleFormatterOptions
        {
            ServiceName = "oficina-mecanica-billing",
            EnvironmentName = "Testing",
            UseUtcTimestamp = true
        };
        using var formatter = new JsonConsoleFormatter(new OptionsMonitorFixo(options));
        var state = argumento is null
            ? new FormattedLogValuesStub(template)
            : new FormattedLogValuesStub(template, argumento);
        var entry = new LogEntry<FormattedLogValuesStub>(
            LogLevel.Information, "Teste", new EventId(1), state, excecao, (s, _) => s.ToString());
        using var writer = new StringWriter();

        formatter.Write(in entry, _scopes, writer);

        return JsonDocument.Parse(writer.ToString()).RootElement.Clone();
    }

    private sealed class FormattedLogValuesStub(string template, params object?[] argumentos)
        : IReadOnlyList<KeyValuePair<string, object?>>
    {
        private readonly List<KeyValuePair<string, object?>> _valores = Montar(template, argumentos);

        public KeyValuePair<string, object?> this[int index] => _valores[index];
        public int Count => _valores.Count;
        public IEnumerator<KeyValuePair<string, object?>> GetEnumerator() => _valores.GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

        public override string ToString()
        {
            var texto = template;
            foreach (var valor in _valores.Where(item => item.Key != "{OriginalFormat}"))
                texto = texto.Replace($"{{{valor.Key}}}", Convert.ToString(valor.Value));
            return texto;
        }

        private static List<KeyValuePair<string, object?>> Montar(string template, object?[] argumentos)
        {
            var nomes = System.Text.RegularExpressions.Regex.Matches(template, @"\{(\w+)\}")
                .Select(match => match.Groups[1].Value)
                .ToList();
            var valores = nomes.Select((nome, indice) =>
                new KeyValuePair<string, object?>(nome, argumentos[indice])).ToList();
            valores.Add(new KeyValuePair<string, object?>("{OriginalFormat}", template));
            return valores;
        }
    }

    private sealed class OptionsMonitorFixo(JsonConsoleFormatterOptions valor)
        : IOptionsMonitor<JsonConsoleFormatterOptions>
    {
        public JsonConsoleFormatterOptions CurrentValue => valor;
        public JsonConsoleFormatterOptions Get(string? name) => valor;
        public IDisposable? OnChange(Action<JsonConsoleFormatterOptions, string?> listener) => null;
    }
}
