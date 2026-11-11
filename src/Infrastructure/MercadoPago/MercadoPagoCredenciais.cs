using System.Text.Json;

namespace Fiap.TechChallenge.OficinaMecanica.Billing.Infrastructure.MercadoPago;

public sealed record MercadoPagoCredenciais(string AccessToken, string WebhookSecret)
{
    // Evita que o valor apareca em log ou em mensagem de excecao por acidente.
    public override string ToString() => "MercadoPagoCredenciais { *** }";
}

/// <summary>
/// Resolve as credenciais do Mercado Pago, no mesmo padrao do
/// <c>JwtSigningKeyResolver</c> do OS Service:
///
/// 1. <c>MercadoPago:SecretId</c> - nome do segredo no Secrets Manager, com o
///    JSON <c>{"access_token": "...", "webhook_secret": "..."}</c>. Caminho do cluster.
/// 2. <c>MercadoPago:AccessToken</c> e <c>MercadoPago:WebhookSecret</c> - execucao
///    local, vindos de user-secrets ou variaveis de ambiente.
///
/// Sem nenhuma das duas o startup falha. Nao existe valor padrao.
/// </summary>
public static class MercadoPagoCredenciaisResolver
{
    public static MercadoPagoCredenciais Resolver(
        MercadoPagoOptions options,
        Func<string, string> lerSegredo)
    {
        if (!string.IsNullOrWhiteSpace(options.SecretId))
            return LerJson(options.SecretId, lerSegredo(options.SecretId));

        if (!string.IsNullOrWhiteSpace(options.AccessToken) &&
            !string.IsNullOrWhiteSpace(options.WebhookSecret))
            return new MercadoPagoCredenciais(options.AccessToken, options.WebhookSecret);

        throw new InvalidOperationException(
            "Credenciais do Mercado Pago nao configuradas. Defina MercadoPago:SecretId com o " +
            "nome do segredo no Secrets Manager, ou MercadoPago:AccessToken e " +
            "MercadoPago:WebhookSecret para execucao local.");
    }

    private static MercadoPagoCredenciais LerJson(string secretId, string conteudo)
    {
        if (string.IsNullOrWhiteSpace(conteudo))
            throw new InvalidOperationException($"O segredo '{secretId}' existe mas esta vazio.");

        try
        {
            using var documento = JsonDocument.Parse(conteudo);
            var accessToken = LerCampo(documento.RootElement, "access_token");
            var webhookSecret = LerCampo(documento.RootElement, "webhook_secret");
            if (accessToken is not null && webhookSecret is not null)
                return new MercadoPagoCredenciais(accessToken, webhookSecret);
        }
        catch (JsonException)
        {
            // Cai na mensagem abaixo. A excecao original nao e propagada porque
            // pode conter trechos do valor do segredo.
        }

        // A mensagem nomeia o segredo e as chaves esperadas, nunca o valor.
        throw new InvalidOperationException(
            $"O segredo '{secretId}' precisa ser um JSON com as chaves " +
            "'access_token' e 'webhook_secret' preenchidas.");
    }

    private static string? LerCampo(JsonElement raiz, string nome) =>
        raiz.ValueKind == JsonValueKind.Object &&
        raiz.TryGetProperty(nome, out var valor) &&
        valor.ValueKind == JsonValueKind.String &&
        !string.IsNullOrWhiteSpace(valor.GetString())
            ? valor.GetString()
            : null;
}
