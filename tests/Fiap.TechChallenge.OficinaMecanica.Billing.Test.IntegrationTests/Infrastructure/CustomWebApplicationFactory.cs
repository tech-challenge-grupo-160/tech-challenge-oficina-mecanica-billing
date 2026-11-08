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
    public InMemoryPagamentoRepository Pagamentos { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IOrcamentoRepository>();
            services.RemoveAll<IPagamentoRepository>();
            services.AddSingleton<IOrcamentoRepository>(Repository);
            services.AddSingleton<IPagamentoRepository>(Pagamentos);
        });
    }
}

public sealed class InMemoryPagamentoRepository : IPagamentoRepository
{
    public Pagamento? Pagamento { get; set; }

    public Task<(Pagamento Pagamento, bool Criado)> CriarOuObterAsync(
        Pagamento candidato,
        Guid mensagemEntradaId,
        CancellationToken cancellationToken)
    {
        var criado = Pagamento is null;
        Pagamento ??= candidato;
        return Task.FromResult((Pagamento, criado));
    }

    public Task<Pagamento?> ObterPorOrdemServicoIdAsync(
        int ordemServicoId,
        CancellationToken cancellationToken) =>
        Task.FromResult(Pagamento?.OrdemServicoId == ordemServicoId ? Pagamento : null);

    public Task<Pagamento?> ObterAsync(
        int ordemServicoId,
        Guid pagamentoId,
        CancellationToken cancellationToken) =>
        Task.FromResult(Pagamento is not null &&
            Pagamento.OrdemServicoId == ordemServicoId &&
            Pagamento.Id == pagamentoId ? Pagamento : null);

    public Task<(Pagamento Pagamento, bool Alterado)> ConfirmarAsync(
        Pagamento pagamento,
        PagamentoConfirmadoMessage evento,
        CancellationToken cancellationToken)
    {
        Pagamento = pagamento;
        return Task.FromResult((pagamento, true));
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

    public Task<(Orcamento Orcamento, bool Alterado)> RegistrarDecisaoAsync(
        Orcamento orcamento,
        Guid eventoId,
        string tipoEvento,
        string payloadEvento,
        CancellationToken cancellationToken)
    {
        Orcamento = orcamento;
        return Task.FromResult((orcamento, true));
    }
}
