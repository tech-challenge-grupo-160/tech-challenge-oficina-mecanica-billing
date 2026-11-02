using Fiap.TechChallenge.OficinaMecanica.Billing.Application.Abstractions;
using Fiap.TechChallenge.OficinaMecanica.Billing.Application.Messaging;
using Fiap.TechChallenge.OficinaMecanica.Billing.Domain.Entities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Fiap.TechChallenge.OficinaMecanica.Billing.Test.IntegrationTests.Infrastructure;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    public InMemoryOrcamentoRepository Repository { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IOrcamentoRepository>();
            services.AddSingleton<IOrcamentoRepository>(Repository);
        });
    }
}

public sealed class InMemoryOrcamentoRepository : IOrcamentoRepository
{
    public Orcamento? Orcamento { get; set; }

    public Task<(Orcamento Orcamento, bool Criado)> CriarOuObterAsync(
        Orcamento candidato,
        Guid mensagemEntradaId,
        OrcamentoGeradoMessage eventoSaida,
        CancellationToken cancellationToken)
    {
        var criado = Orcamento is null;
        Orcamento ??= candidato;
        return Task.FromResult((Orcamento, criado));
    }

    public Task<Orcamento?> ObterPorOrdemServicoIdAsync(
        int ordemServicoId,
        CancellationToken cancellationToken) =>
        Task.FromResult(Orcamento?.OrdemServicoId == ordemServicoId ? Orcamento : null);
}
