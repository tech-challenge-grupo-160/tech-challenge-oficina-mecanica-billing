using System.Text.RegularExpressions;

namespace Fiap.TechChallenge.OficinaMecanica.Billing.Infrastructure.Logging;

public static partial class SensitiveDataMasker
{
    public static string Mask(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value ?? string.Empty;
        }

        // CNPJ antes do CPF: os 11 primeiros digitos de um CNPJ sem pontuacao
        // nao podem virar um "CPF" mascarado pela metade.
        var masked = CnpjRegex().Replace(value, "**.***.***/****-**");
        masked = CpfRegex().Replace(masked, "***.***.***-**");
        masked = BearerTokenRegex().Replace(masked, "$1[REDACTED]");
        masked = JwtRegex().Replace(masked, "[REDACTED]");
        return SensitiveKeyValueRegex().Replace(masked, "$1[REDACTED]");
    }

    [GeneratedRegex(@"(?<!\d)\d{2}\.?\d{3}\.?\d{3}/?\d{4}-?\d{2}(?!\d)", RegexOptions.CultureInvariant)]
    private static partial Regex CnpjRegex();

    [GeneratedRegex(@"(?<!\d)\d{3}\.?\d{3}\.?\d{3}-?\d{2}(?!\d)", RegexOptions.CultureInvariant)]
    private static partial Regex CpfRegex();

    [GeneratedRegex(@"(?i)(\bBearer\s+)[A-Za-z0-9\-._~+/]+=*", RegexOptions.CultureInvariant)]
    private static partial Regex BearerTokenRegex();

    [GeneratedRegex(@"\b[A-Za-z0-9_-]{20,}\.[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}\b", RegexOptions.CultureInvariant)]
    private static partial Regex JwtRegex();

    [GeneratedRegex(@"(?i)([""']?\b(?:authorization|api[_-]?key|token|access[_-]?token|refresh[_-]?token|password|senha|secret)\b[""']?\s*[:=]\s*[""']?)[^,""'\s}]+", RegexOptions.CultureInvariant)]
    private static partial Regex SensitiveKeyValueRegex();
}
