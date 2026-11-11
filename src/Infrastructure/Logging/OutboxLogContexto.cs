using System.Text.Json;

namespace Fiap.TechChallenge.OficinaMecanica.Billing.Infrastructure.Logging;

/// <summary>
/// Le o numero da OS do payload de um evento da Outbox, para o log da
/// publicacao sair com o mesmo contexto do processamento que gerou o evento.
/// </summary>
public static class OutboxLogContexto
{
    public static int? LerOrdemServicoId(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
            return null;

        try
        {
            using var documento = JsonDocument.Parse(payload);
            return documento.RootElement.ValueKind == JsonValueKind.Object &&
                   (documento.RootElement.TryGetProperty("ordemServicoId", out var valor) ||
                    documento.RootElement.TryGetProperty("OrdemServicoId", out valor)) &&
                   valor.ValueKind == JsonValueKind.Number &&
                   valor.TryGetInt32(out var ordemServicoId)
                ? ordemServicoId
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
