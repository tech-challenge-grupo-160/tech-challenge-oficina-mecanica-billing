using Microsoft.Extensions.DependencyInjection;

namespace Fiap.TechChallenge.OficinaMecanica.Billing.Infrastructure;

// O banco, a mensageria e os health checks deles entram nas tasks de cada
// dependencia; por enquanto o servico so responde se o processo esta de pe.
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddHealthChecks();

        return services;
    }
}
