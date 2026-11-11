using Fiap.TechChallenge.OficinaMecanica.Billing.Infrastructure.Logging;
using FluentAssertions;

namespace Fiap.TechChallenge.OficinaMecanica.Billing.Test.UnitTests.Units.Infrastructure;

public sealed class SensitiveDataMaskerTests
{
    [Theory]
    [InlineData("cpf 123.456.789-09", "cpf ***.***.***-**")]
    [InlineData("cpf 12345678909", "cpf ***.***.***-**")]
    [InlineData("/api/v1/clientes/documento/12345678909", "/api/v1/clientes/documento/***.***.***-**")]
    [InlineData("cnpj 12.345.678/0001-95", "cnpj **.***.***/****-**")]
    [InlineData("cnpj 12345678000195", "cnpj **.***.***/****-**")]
    [InlineData("token Bearer abc.def.ghi", "token Bearer [REDACTED]")]
    [InlineData("access_token=APP_USR-123", "access_token=[REDACTED]")]
    [InlineData("OS 3007 com 2 pecas", "OS 3007 com 2 pecas")]
    [InlineData("", "")]
    public void Mask_DeveOcultarDadosSensiveis(string entrada, string esperado) =>
        SensitiveDataMasker.Mask(entrada).Should().Be(esperado);

    [Fact]
    public void Mask_DeveAceitarNulo() =>
        SensitiveDataMasker.Mask(null).Should().BeEmpty();
}
