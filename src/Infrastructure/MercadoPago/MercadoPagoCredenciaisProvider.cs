using System.Diagnostics.CodeAnalysis;
using Amazon;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Fiap.TechChallenge.OficinaMecanica.Billing.Infrastructure.MercadoPago;

/// <summary>
/// Le as credenciais uma vez e guarda em memoria. Trocar o segredo exige
/// reiniciar os pods (mesma limitacao da chave do JWT no OS Service).
/// </summary>
public sealed class MercadoPagoCredenciaisProvider
{
    private readonly Lazy<MercadoPagoCredenciais> _credenciais;

    public MercadoPagoCredenciaisProvider(
        IOptions<MercadoPagoOptions> options,
        Func<string, string>? lerSegredo = null)
    {
        _credenciais = new Lazy<MercadoPagoCredenciais>(() =>
            MercadoPagoCredenciaisResolver.Resolver(
                options.Value,
                lerSegredo ?? (secretId => LerDoSecretsManager(secretId, options.Value.Region))));
    }

    public MercadoPagoCredenciais Obter() => _credenciais.Value;

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
/// Resolve as credenciais no startup quando a integracao esta ligada: sem
/// credencial valida o pod nao sobe, com uma mensagem clara no log.
/// </summary>
public sealed class MercadoPagoCredenciaisStartupCheck(
    MercadoPagoCredenciaisProvider provider,
    IOptions<MercadoPagoOptions> options) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (options.Value.Enabled)
            provider.Obter();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
