using System.Security.Cryptography;
using System.Text;
using Fiap.TechChallenge.OficinaMecanica.Billing.Infrastructure.MercadoPago;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace Fiap.TechChallenge.OficinaMecanica.Billing.Test.UnitTests.Units.Infrastructure;

public sealed class MercadoPagoAssinaturaValidatorTests
{
    private const string Segredo = "segredo-de-teste";

    [Fact]
    public void Validar_DeveAceitarAssinaturaCalculadaComOSegredo()
    {
        var header = Assinar("id:123;request-id:req-1;ts:1704908010;", "1704908010");

        MercadoPagoAssinaturaValidator.Validar(Segredo, header, "req-1", "123").Should().BeTrue();
    }

    [Fact]
    public void Validar_DeveUsarDataIdEmMinusculas()
    {
        var header = Assinar("id:abc123;request-id:req-1;ts:1;", "1");

        MercadoPagoAssinaturaValidator.Validar(Segredo, header, "req-1", "ABC123").Should().BeTrue();
    }

    [Fact]
    public void Validar_DeveOmitirPartesAusentesDoManifesto()
    {
        var header = Assinar("ts:1;", "1");

        MercadoPagoAssinaturaValidator.Validar(Segredo, header, null, null).Should().BeTrue();
    }

    [Fact]
    public void Validar_DeveAceitarEspacosEOrdemDiferenteNoHeader()
    {
        var hmac = Hmac("id:123;request-id:req-1;ts:1;");

        MercadoPagoAssinaturaValidator.Validar(Segredo, $"v1={hmac}, ts=1", "req-1", "123")
            .Should().BeTrue();
    }

    [Theory]
    [InlineData("456", "req-1")]
    [InlineData("123", "req-2")]
    public void Validar_DeveRecusarQuandoIdOuRequestMudam(string dataId, string requestId)
    {
        var header = Assinar("id:123;request-id:req-1;ts:1;", "1");

        MercadoPagoAssinaturaValidator.Validar(Segredo, header, requestId, dataId).Should().BeFalse();
    }

    [Fact]
    public void Validar_DeveRecusarSegredoDiferente()
    {
        var header = Assinar("id:123;request-id:req-1;ts:1;", "1");

        MercadoPagoAssinaturaValidator.Validar("outro-segredo", header, "req-1", "123").Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("v1=abc")]
    [InlineData("ts=1")]
    [InlineData("ts=1,v1=nao-hex")]
    [InlineData("lixo")]
    public void Validar_DeveRecusarHeaderInvalido(string? header) =>
        MercadoPagoAssinaturaValidator.Validar(Segredo, header, "req-1", "123").Should().BeFalse();

    [Fact]
    public void Validar_DeveRecusarSemSegredo() =>
        MercadoPagoAssinaturaValidator.Validar("", Assinar("ts:1;", "1"), null, null).Should().BeFalse();

    [Fact]
    public void Instancia_DeveUsarSegredoDasCredenciais()
    {
        var provider = new MercadoPagoCredenciaisProvider(
            Options.Create(new MercadoPagoOptions { AccessToken = "TEST-token", WebhookSecret = Segredo }));
        var validator = new MercadoPagoAssinaturaValidator(provider);

        validator.Validar(Assinar("id:123;ts:1;", "1"), null, "123").Should().BeTrue();
    }

    private static string Assinar(string manifesto, string ts) => $"ts={ts},v1={Hmac(manifesto)}";

    private static string Hmac(string manifesto) =>
        Convert.ToHexStringLower(HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(Segredo),
            Encoding.UTF8.GetBytes(manifesto)));
}
