using System.Globalization;

namespace Fiap.TechChallenge.OficinaMecanica.Billing.Application.Pagamentos;

/// <summary>
/// Referencia enviada ao Mercado Pago como <c>external_reference</c>. Liga a
/// cobranca a OS e ao pagamento do Billing, e e o que permite localizar o
/// pagamento quando a notificacao chega.
/// </summary>
public static class ReferenciaExterna
{
    private const string PrefixoOrdem = "os:";
    private const string PrefixoPagamento = "pagamento:";

    public static string Criar(int ordemServicoId, Guid pagamentoId) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{PrefixoOrdem}{ordemServicoId};{PrefixoPagamento}{pagamentoId:D}");

    public static bool TentarLer(string? valor, out int ordemServicoId, out Guid pagamentoId)
    {
        ordemServicoId = 0;
        pagamentoId = Guid.Empty;
        if (string.IsNullOrWhiteSpace(valor))
            return false;

        var partes = valor.Split(';');
        if (partes.Length != 2 ||
            !partes[0].StartsWith(PrefixoOrdem, StringComparison.Ordinal) ||
            !partes[1].StartsWith(PrefixoPagamento, StringComparison.Ordinal))
            return false;

        return int.TryParse(
                   partes[0][PrefixoOrdem.Length..],
                   NumberStyles.None,
                   CultureInfo.InvariantCulture,
                   out ordemServicoId) &&
               ordemServicoId > 0 &&
               Guid.TryParse(partes[1][PrefixoPagamento.Length..], out pagamentoId) &&
               pagamentoId != Guid.Empty;
    }
}
