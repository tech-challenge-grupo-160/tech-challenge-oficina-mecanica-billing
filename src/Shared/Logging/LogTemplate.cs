using System.Diagnostics;

namespace Fiap.TechChallenge.OficinaMecanica.Billing.Shared.Logging;

public static class LogTemplate
{
    public const string Start = "[Iniciando] componente: {componente}.";
    public const string End = "[Finalizando] componente: {componente}. | {message}";
    public const string Trace = "[Executando] -> componente: {componente}, method: {method} | {message}.";
    public const string Error = "[Erro] componente: {componente}, method: {method}, traceId: {traceId} | {message}.";
    public const string Warning = "[Warning] componente: {componente}, method: {method} | {message}.";

    public static string CurrentTraceId() => Activity.Current?.TraceId.ToString() ?? "n/a";
}
