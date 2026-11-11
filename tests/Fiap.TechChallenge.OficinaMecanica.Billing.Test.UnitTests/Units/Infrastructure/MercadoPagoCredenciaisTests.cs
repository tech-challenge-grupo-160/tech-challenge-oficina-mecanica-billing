using Fiap.TechChallenge.OficinaMecanica.Billing.Infrastructure.MercadoPago;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace Fiap.TechChallenge.OficinaMecanica.Billing.Test.UnitTests.Units.Infrastructure;

public sealed class MercadoPagoCredenciaisTests
{
    private const string SegredoValido = """{"access_token":"TEST-token","webhook_secret":"segredo-teste"}""";

    [Fact]
    public void Resolver_DeveLerDoSecretsManagerQuandoSecretIdForInformado()
    {
        string? lido = null;

        var credenciais = MercadoPagoCredenciaisResolver.Resolver(
            new MercadoPagoOptions { SecretId = "tc-grupo160/dev/mercadopago" },
            id => { lido = id; return SegredoValido; });

        lido.Should().Be("tc-grupo160/dev/mercadopago");
        credenciais.AccessToken.Should().Be("TEST-token");
        credenciais.WebhookSecret.Should().Be("segredo-teste");
    }

    [Fact]
    public void Resolver_DevePreferirOSecretsManagerQuandoAsDuasOrigensExistirem()
    {
        var credenciais = MercadoPagoCredenciaisResolver.Resolver(
            new MercadoPagoOptions
            {
                SecretId = "tc-grupo160/dev/mercadopago",
                AccessToken = "local",
                WebhookSecret = "local"
            },
            _ => SegredoValido);

        credenciais.AccessToken.Should().Be("TEST-token");
    }

    [Fact]
    public void Resolver_DeveUsarConfiguracaoLocalSemSecretId()
    {
        var credenciais = MercadoPagoCredenciaisResolver.Resolver(
            new MercadoPagoOptions { AccessToken = "TEST-local", WebhookSecret = "segredo-local" },
            _ => throw new InvalidOperationException("nao deve ler"));

        credenciais.Should().Be(new MercadoPagoCredenciais("TEST-local", "segredo-local"));
    }

    [Fact]
    public void Resolver_DeveFalharSemCredenciais()
    {
        var action = () => MercadoPagoCredenciaisResolver.Resolver(
            new MercadoPagoOptions { AccessToken = "so-o-token" }, _ => SegredoValido);

        action.Should().Throw<InvalidOperationException>().WithMessage("*MercadoPago:SecretId*");
    }

    [Theory]
    [InlineData("")]
    [InlineData("nao-e-json")]
    [InlineData("[]")]
    [InlineData("""{"access_token":"TEST-token"}""")]
    [InlineData("""{"access_token":"TEST-token","webhook_secret":""}""")]
    [InlineData("""{"access_token":1,"webhook_secret":"segredo"}""")]
    public void Resolver_DeveFalharComSegredoInvalidoSemExporValor(string conteudo)
    {
        var action = () => MercadoPagoCredenciaisResolver.Resolver(
            new MercadoPagoOptions { SecretId = "tc-grupo160/dev/mercadopago" }, _ => conteudo);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*tc-grupo160/dev/mercadopago*")
            .Which.InnerException.Should().BeNull();
    }

    [Fact]
    public void Credenciais_NaoDevemAparecerNoToString() =>
        new MercadoPagoCredenciais("TEST-token", "segredo").ToString()
            .Should().NotContain("TEST-token").And.NotContain("segredo");

    [Fact]
    public void Provider_DeveLerOSegredoUmaVez()
    {
        var leituras = 0;
        var provider = new MercadoPagoCredenciaisProvider(
            Options.Create(new MercadoPagoOptions { SecretId = "tc-grupo160/dev/mercadopago" }),
            _ => { leituras++; return SegredoValido; });

        provider.Obter();
        provider.Obter();

        leituras.Should().Be(1);
    }

    [Fact]
    public async Task StartupCheck_DeveFalharQuandoHabilitadoSemCredenciais()
    {
        var options = Options.Create(new MercadoPagoOptions { Enabled = true });
        var check = new MercadoPagoCredenciaisStartupCheck(
            new MercadoPagoCredenciaisProvider(options, _ => string.Empty), options);

        var action = () => check.StartAsync(CancellationToken.None);

        await action.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task StartupCheck_NaoDeveLerQuandoDesabilitado()
    {
        var options = Options.Create(new MercadoPagoOptions { Enabled = false });
        var check = new MercadoPagoCredenciaisStartupCheck(
            new MercadoPagoCredenciaisProvider(options, _ => throw new InvalidOperationException()), options);

        await check.StartAsync(CancellationToken.None);
        await check.StopAsync(CancellationToken.None);
    }
}
