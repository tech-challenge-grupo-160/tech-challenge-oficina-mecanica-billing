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
///
/// A leitura no cluster e assincrona e tem tempo limite: quem carrega e o
/// <see cref="MercadoPagoCredenciaisStartupCheck"/>, com await, antes do Kestrel
/// subir. Ate 15/11/2026 a leitura era sincrona (GetAwaiter().GetResult()
/// dentro de um Lazy) e, com o segredo preenchido, o pod travava no startup sem
/// escrever nenhum log nem abrir a porta 8080.
/// </summary>
public sealed class MercadoPagoCredenciaisProvider
{
    /// <summary>Tempo maximo para ler o segredo no startup.</summary>
    public static readonly TimeSpan TempoLimitePadrao = TimeSpan.FromSeconds(15);

    private readonly MercadoPagoOptions _options;
    private readonly Func<string, CancellationToken, Task<string>> _lerSegredo;
    private readonly TimeSpan _tempoLimite;
    private readonly SemaphoreSlim _carga = new(1, 1);
    private volatile Resultado? _resultado;

    /// <summary>
    /// Leitura sincrona injetada (testes) ou, sem ela, o Secrets Manager.
    /// </summary>
    public MercadoPagoCredenciaisProvider(
        IOptions<MercadoPagoOptions> options,
        Func<string, string>? lerSegredo = null)
        : this(
            options,
            lerSegredo is null ? null : (secretId, _) => Task.FromResult(lerSegredo(secretId)),
            TempoLimitePadrao)
    {
    }

    public MercadoPagoCredenciaisProvider(
        IOptions<MercadoPagoOptions> options,
        Func<string, CancellationToken, Task<string>>? lerSegredoAsync,
        TimeSpan tempoLimite)
    {
        _options = options.Value;
        _lerSegredo = lerSegredoAsync ?? ((secretId, ct) => LerDoSecretsManagerAsync(secretId, _options.Region, ct));
        _tempoLimite = tempoLimite;
    }

    /// <summary>Ha credencial valida para falar com o Mercado Pago.</summary>
    public bool Disponivel => Atual.Credenciais is not null;

    /// <summary>Por que a credencial nao esta disponivel; null quando esta.</summary>
    public string? Falha => Atual.Falha;

    public MercadoPagoCredenciais Obter() =>
        Atual.Credenciais ?? throw new InvalidOperationException(Atual.Falha);

    /// <summary>
    /// Le e guarda as credenciais, inclusive a falha. Chamadas seguintes nao
    /// leem de novo: trocar ou corrigir o segredo exige reiniciar os pods.
    /// </summary>
    public async Task CarregarAsync(CancellationToken cancellationToken = default)
    {
        if (_resultado is not null)
            return;

        await _carga.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_resultado is not null)
                return;

            _resultado = await ResolverAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _carga.Release();
        }
    }

    // Fora do cluster (testes, execucao local com AccessToken/WebhookSecret) a
    // leitura e sincrona e termina na hora. No cluster o StartupCheck ja carregou
    // antes de qualquer uso, porque e registrado antes dos workers.
    private Resultado Atual
    {
        get
        {
            if (_resultado is null)
                CarregarAsync().GetAwaiter().GetResult();

            return _resultado!;
        }
    }

    private async Task<Resultado> ResolverAsync(CancellationToken cancellationToken)
    {
        using var limite = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limite.CancelAfter(_tempoLimite);

        try
        {
            string? conteudo = null;
            if (!string.IsNullOrWhiteSpace(_options.SecretId))
            {
                // WaitAsync garante o tempo limite mesmo que a leitura ignore o token.
                conteudo = await _lerSegredo(_options.SecretId, limite.Token)
                    .WaitAsync(limite.Token)
                    .ConfigureAwait(false);
            }

            var credenciais = MercadoPagoCredenciaisResolver.Resolver(_options, _ => conteudo ?? string.Empty);
            return new Resultado(credenciais, null);
        }
        catch (InvalidOperationException ex)
        {
            // As mensagens do resolver e da leitura nomeiam o segredo e as
            // chaves esperadas, nunca o valor.
            return new Resultado(null, ex.Message);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new Resultado(
                null,
                $"Tempo esgotado ({_tempoLimite.TotalSeconds:0} s) ao ler o segredo '{_options.SecretId}' no Secrets Manager.");
        }
    }

    [ExcludeFromCodeCoverage]
    private static async Task<string> LerDoSecretsManagerAsync(
        string secretId,
        string region,
        CancellationToken cancellationToken)
    {
        try
        {
            using var client = new AmazonSecretsManagerClient(RegionEndpoint.GetBySystemName(region));
            var response = await client
                .GetSecretValueAsync(new GetSecretValueRequest { SecretId = secretId }, cancellationToken)
                .ConfigureAwait(false);
            return response.SecretString ?? string.Empty;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // A mensagem nomeia o segredo, nunca o valor.
            throw new InvalidOperationException(
                $"Falha ao ler o segredo '{secretId}' no Secrets Manager.", ex);
        }
    }

    private sealed record Resultado(MercadoPagoCredenciais? Credenciais, string? Falha);
}

/// <summary>
/// Carrega as credenciais no startup, com await e tempo limite, quando a
/// integracao esta ligada. Sem credencial valida o pod sobe mesmo assim, sem
/// cobranca no Mercado Pago, e o motivo fica registrado no log logo na subida.
/// </summary>
public sealed class MercadoPagoCredenciaisStartupCheck(
    MercadoPagoCredenciaisProvider provider,
    IOptions<MercadoPagoOptions> options,
    ILogger<MercadoPagoCredenciaisStartupCheck> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled)
            return;

        await provider.CarregarAsync(cancellationToken).ConfigureAwait(false);

        if (provider.Disponivel)
            logger.LogInformation("Credenciais do Mercado Pago carregadas.");
        else
            logger.LogWarning(
                "Integracao com o Mercado Pago ligada, mas sem credencial valida: {Falha} " +
                "O Billing segue sem cobranca no Mercado Pago: os orcamentos funcionam e o webhook " +
                "responde 404. Preencha o segredo e reinicie os pods.",
                provider.Falha);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
