namespace Fiap.TechChallenge.OficinaMecanica.Billing.Application.Abstractions;

/// <summary>
/// Valida o header <c>x-signature</c> das notificacoes do Mercado Pago com a
/// assinatura secreta do webhook.
/// </summary>
public interface IMercadoPagoAssinaturaValidator
{
    bool Validar(string? xSignature, string? xRequestId, string? dataId);
}
