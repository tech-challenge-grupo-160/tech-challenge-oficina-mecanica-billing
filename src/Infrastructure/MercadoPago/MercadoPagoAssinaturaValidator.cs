using System.Security.Cryptography;
using System.Text;
using Fiap.TechChallenge.OficinaMecanica.Billing.Application.Abstractions;

namespace Fiap.TechChallenge.OficinaMecanica.Billing.Infrastructure.MercadoPago;

/// <summary>
/// Valida o <c>x-signature</c> conforme a documentacao de webhooks do Mercado Pago.
///
/// O header tem o formato <c>ts=&lt;timestamp&gt;,v1=&lt;hmac&gt;</c>. O HMAC-SHA256
/// (hex) e calculado com a assinatura secreta sobre o manifesto
/// <c>id:&lt;data.id&gt;;request-id:&lt;x-request-id&gt;;ts:&lt;ts&gt;;</c>, onde
/// partes ausentes sao omitidas e o <c>data.id</c> alfanumerico vai em minusculas.
/// </summary>
public sealed class MercadoPagoAssinaturaValidator(MercadoPagoCredenciaisProvider credenciais)
    : IMercadoPagoAssinaturaValidator
{
    public bool Validar(string? xSignature, string? xRequestId, string? dataId) =>
        Validar(credenciais.Obter().WebhookSecret, xSignature, xRequestId, dataId);

    public static bool Validar(
        string segredo,
        string? xSignature,
        string? xRequestId,
        string? dataId)
    {
        if (string.IsNullOrWhiteSpace(segredo) || string.IsNullOrWhiteSpace(xSignature))
            return false;

        string? ts = null;
        string? v1 = null;
        foreach (var parte in xSignature.Split(','))
        {
            var chaveValor = parte.Split('=', 2, StringSplitOptions.TrimEntries);
            if (chaveValor.Length != 2)
                continue;
            if (chaveValor[0] == "ts")
                ts = chaveValor[1];
            else if (chaveValor[0] == "v1")
                v1 = chaveValor[1];
        }

        if (string.IsNullOrWhiteSpace(ts) || string.IsNullOrWhiteSpace(v1))
            return false;

        byte[] esperado;
        try
        {
            esperado = Convert.FromHexString(v1);
        }
        catch (FormatException)
        {
            return false;
        }

        var manifesto = CriarManifesto(dataId, xRequestId, ts);
        var calculado = HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(segredo),
            Encoding.UTF8.GetBytes(manifesto));

        return CryptographicOperations.FixedTimeEquals(calculado, esperado);
    }

    public static string CriarManifesto(string? dataId, string? xRequestId, string ts)
    {
        var manifesto = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(dataId))
            manifesto.Append("id:").Append(dataId.ToLowerInvariant()).Append(';');
        if (!string.IsNullOrWhiteSpace(xRequestId))
            manifesto.Append("request-id:").Append(xRequestId).Append(';');
        manifesto.Append("ts:").Append(ts).Append(';');
        return manifesto.ToString();
    }
}
