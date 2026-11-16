using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Extensions.Logging;

namespace Fiap.TechChallenge.OficinaMecanica.Billing.Shared.Logging;

/// <summary>
/// Contexto que acompanha uma OS entre os servicos (F4-32). Os campos entram
/// como escopo de log, entao todo log escrito dentro do escopo sai com o
/// <c>correlationId</c> e o numero da OS, inclusive os logs de handlers e
/// repositorios que nao conhecem a mensagem.
/// </summary>
public static class LogContexto
{
    public const string CorrelationId = "correlationId";
    public const string OrdemServicoId = "ordemServicoId";
    public const string MessageId = "messageId";
    public const string TipoMensagem = "tipoMensagem";

    /// <summary>Header HTTP que carrega o correlationId entre os servicos.</summary>
    public const string CorrelationIdHeader = "X-Correlation-Id";

    /// <summary>Chaves que o formatter JSON aceita dos escopos.</summary>
    public static readonly IReadOnlySet<string> Campos =
        new HashSet<string>(StringComparer.Ordinal) { CorrelationId, OrdemServicoId, MessageId, TipoMensagem };

    private static readonly ConcurrentDictionary<Type, PropriedadesMensagem> Propriedades = new();

    public static IDisposable? Iniciar(
        this ILogger logger,
        Guid? correlationId = null,
        int? ordemServicoId = null,
        Guid? messageId = null,
        string? tipoMensagem = null)
    {
        var contexto = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (correlationId is { } correlation && correlation != Guid.Empty)
            contexto[CorrelationId] = correlation;
        if (ordemServicoId is > 0)
            contexto[OrdemServicoId] = ordemServicoId.Value;
        if (messageId is { } message && message != Guid.Empty)
            contexto[MessageId] = message;
        if (!string.IsNullOrWhiteSpace(tipoMensagem))
            contexto[TipoMensagem] = tipoMensagem;

        return contexto.Count == 0 ? null : logger.BeginScope(contexto);
    }

    /// <summary>
    /// Abre o escopo a partir de uma mensagem de integracao, lendo as
    /// propriedades <c>CorrelationId</c>, <c>OrdemServicoId</c> e
    /// <c>MessageId</c> quando existirem.
    /// </summary>
    public static IDisposable? IniciarParaMensagem(this ILogger logger, object mensagem, string? tipoMensagem = null)
    {
        var propriedades = Propriedades.GetOrAdd(mensagem.GetType(), PropriedadesMensagem.Ler);
        return logger.Iniciar(
            propriedades.CorrelationId?.GetValue(mensagem) as Guid?,
            propriedades.OrdemServicoId?.GetValue(mensagem) as int?,
            propriedades.MessageId?.GetValue(mensagem) as Guid?,
            tipoMensagem ?? mensagem.GetType().Name);
    }

    private sealed record PropriedadesMensagem(
        PropertyInfo? CorrelationId,
        PropertyInfo? OrdemServicoId,
        PropertyInfo? MessageId)
    {
        public static PropriedadesMensagem Ler(Type tipo) => new(
            Buscar(tipo, "CorrelationId", typeof(Guid)),
            Buscar(tipo, "OrdemServicoId", typeof(int)),
            Buscar(tipo, "MessageId", typeof(Guid)));

        private static PropertyInfo? Buscar(Type tipo, string nome, Type tipoValor)
        {
            var propriedade = tipo.GetProperty(nome, BindingFlags.Public | BindingFlags.Instance);
            return propriedade?.PropertyType == tipoValor ? propriedade : null;
        }
    }
}
