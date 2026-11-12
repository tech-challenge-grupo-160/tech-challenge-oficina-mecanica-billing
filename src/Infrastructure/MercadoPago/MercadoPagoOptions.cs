namespace Fiap.TechChallenge.OficinaMecanica.Billing.Infrastructure.MercadoPago;

public sealed class MercadoPagoOptions
{
    public const string SectionName = "MercadoPago";

    /// <summary>Liga a integracao. Desligada, o pagamento segue o fluxo simulado.</summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Nome do segredo no Secrets Manager (<c>tc-grupo160/&lt;ambiente&gt;/mercadopago</c>).
    /// Caminho usado no cluster: a configuracao carrega o nome, nunca o valor.
    /// </summary>
    public string? SecretId { get; set; }

    /// <summary>Somente execucao local (user-secrets ou variavel de ambiente).</summary>
    public string? AccessToken { get; set; }

    /// <summary>Somente execucao local (user-secrets ou variavel de ambiente).</summary>
    public string? WebhookSecret { get; set; }

    public string Region { get; set; } = "us-east-1";
    public string BaseUrl { get; set; } = "https://api.mercadopago.com";

    /// <summary>
    /// URL publica do webhook enviada na preferencia. Vazio = vale a URL
    /// configurada no painel da aplicacao do Mercado Pago.
    /// </summary>
    public string? NotificationUrl { get; set; }

    public int TimeoutSeconds { get; set; } = 10;
}
