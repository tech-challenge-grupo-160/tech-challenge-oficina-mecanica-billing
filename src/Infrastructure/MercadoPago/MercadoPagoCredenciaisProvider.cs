using System.Diagnostics.CodeAnalysis;
using Amazon;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fiap.TechChallenge.OficinaMecanica.Billing.Infrastructure.MercadoPago;

/// <summary>
/// Le as credenciais uma vez e guarda em memoria. Trocar o segredo exige
/// reiniciar os pods (mesma limitacao da chave do JWT no OS Service).
///
/// Credencial ausente ou invalida nao derruba o servico: <see cref="Disponivel"/>
/// fica false, o gateway se comporta como desligado e o resto do Billing
/// (orcamentos) continua no ar. Um ambiente pode subir sem as credenciais de
/// teste do Mercado Pago - so a cobranca fica de fora.
/// </summary>
public sealed class MercadoPagoCredenciaisProvider
{
    private readonly Lazy<(MercadoPagoCredenciais? Credenciais, string? Falha)> _resultado;

    public MercadoPagoCredenciaisProvider(
        IOptions<MercadoPagoOptions> options,
        Func<string, string>? lerSegredo = null)
    {
        _resultado = new Lazy<(MercadoPagoCredenciais?, string?)>(() =>
        {
            try
            {
                return (MercadoPagoCredenciaisResolver.Resolver(
                    options.Value,
                    lerSegredo ?? (secretId => LerDoSecretsManager(secretId, options.Value.Region))), null);
            }
            catch (InvalidOperationException ex)
            {
                // As mensagens do resolver e da leitura nomeiam o segredo e as
                // chaves esperadas, nunca o valor.
                return (null, ex.Message);
            }
        });
    }

    /// <summary>Ha credencial valida para falar com o Mercado Pago.</summary>
    public bool Disponivel => _resultado.Value.Credenciais is not null;

    /// <summary>Por que a credencial nao esta disponivel; null quando esta.</summary>
    public string? Falha => _resultado.Value.Falha;

    public MercadoPagoCredenciais Obter() =>
        _resultado.Value.Credenciais ?? throw new InvalidOperationException(_resultado.Value.Falha);

    [ExcludeFromCodeCoverage]
    private static string LerDoSecretsManager(string secretId, string region)
    {
        try
        {
            using var client = new AmazonSecretsManagerClient(RegionEndpoint.GetBySystemName(region));
            var response = client
                .GetSecretValueAsync(new GetSecretValueRequest { SecretId = secretId })
                .GetAwaiter()
                .GetResult();
            return response.SecretString ?? string.Empty;
        }
        catch (Exception ex)
        {
            // A mensagem nomeia o segredo, nunca o valor.
            throw new InvalidOperationException(
                $"Falha ao ler o segredo '{secretId}' no Secrets Manager.", ex);
        }
    }
}

/// <summary>
/// Resolve as credenciais no startup quando a integracao esta ligada. Sem
/// credencial valida o pod sobe mesmo assim, sem cobranca no Mercado Pago, e o
/// motivo fica registrado no log logo na subida.
/// </summary>
public sealed class MercadoPagoCredenciaisStartupCheck(
    MercadoPagoCredenciaisProvider provider,
    IOptions<MercadoPagoOptions> options,
    ILogger<MercadoPagoCredenciaisStartupCheck> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (options.Value.Enabled && !provider.Disponivel)
            logger.LogWarning(
                "Integracao com o Mercado Pago ligada, mas sem credencial valida: {Falha} " +
                "O Billing segue sem cobranca no Mercado Pago: os orcamentos funcionam e o webhook " +
                "responde 404. Preencha o segredo e reinicie os pods.",
                provider.Falha);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
