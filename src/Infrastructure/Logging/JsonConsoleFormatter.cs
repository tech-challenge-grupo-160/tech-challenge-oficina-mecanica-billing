using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;

namespace Fiap.TechChallenge.OficinaMecanica.Billing.Infrastructure.Logging;

public sealed class JsonConsoleFormatter : ConsoleFormatter
{
    public const string FormatterName = "structured-json";

    private readonly IDisposable? _optionsReloadToken;
    private JsonConsoleFormatterOptions _options;

    public JsonConsoleFormatter(IOptionsMonitor<JsonConsoleFormatterOptions> options)
        : base(FormatterName)
    {
        _options = options.CurrentValue;
        _optionsReloadToken = options.OnChange(updated => _options = updated);
    }

    public override void Write<TState>(
        in LogEntry<TState> logEntry,
        IExternalScopeProvider? scopeProvider,
        TextWriter textWriter)
    {
        var state = ReadState(logEntry.State);
        var renderedMessage = logEntry.Formatter?.Invoke(logEntry.State, logEntry.Exception) ?? string.Empty;
        var log = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["timestamp"] = (_options.UseUtcTimestamp ? DateTimeOffset.UtcNow : DateTimeOffset.Now).ToString("O"),
            ["nivel"] = ToLevelLabel(logEntry.LogLevel),
            ["mensagem"] = SensitiveDataMasker.Mask(renderedMessage),
            ["servico"] = _options.ServiceName,
            ["ambiente"] = _options.EnvironmentName,
            ["rota"] = GetValue(state, "rota"),
            ["status"] = GetValue(state, "status"),
            ["duracao"] = GetValue(state, "duracao"),
            ["traceId"] = GetValue(state, "traceId")
        };

        foreach (var property in state)
        {
            if (property.Key is "{OriginalFormat}" or "rota" or "status" or "duracao" or "traceId")
            {
                continue;
            }

            log[property.Key] = MaskValue(property.Value);
        }

        if (logEntry.Exception is not null)
        {
            log["erro"] = SensitiveDataMasker.Mask(logEntry.Exception.ToString());
        }

        textWriter.WriteLine(JsonSerializer.Serialize(log));
    }

    private static Dictionary<string, object?> ReadState<TState>(TState state)
    {
        var values = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (state is IEnumerable<KeyValuePair<string, object?>> properties)
        {
            foreach (var property in properties)
            {
                values[property.Key] = property.Value;
            }
        }

        return values;
    }

    private static object? GetValue(IReadOnlyDictionary<string, object?> state, string key)
    {
        return state.TryGetValue(key, out var value) ? MaskValue(value) : null;
    }

    private static object? MaskValue(object? value)
    {
        return value switch
        {
            null => null,
            string text => SensitiveDataMasker.Mask(text),
            byte or sbyte or short or ushort or int or uint or long or ulong
                or float or double or decimal or bool or char
                or DateTime or DateTimeOffset or TimeSpan or Guid => value,
            _ => "[REDACTED]"
        };
    }

    private static string ToLevelLabel(LogLevel logLevel)
    {
        return logLevel switch
        {
            LogLevel.Trace => "trace",
            LogLevel.Debug => "debug",
            LogLevel.Information => "information",
            LogLevel.Warning => "warning",
            LogLevel.Error => "error",
            LogLevel.Critical => "critical",
            _ => "none"
        };
    }
}
