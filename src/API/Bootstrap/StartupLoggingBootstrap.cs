using Fiap.TechChallenge.OficinaMecanica.Billing.Shared.Logging;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;

namespace Fiap.TechChallenge.OficinaMecanica.Billing.API.Bootstrap;

public static class StartupLoggingBootstrap
{
    private const string StartupLoggerName = "Startup";

    public static WebApplication LogStartupUrls(this WebApplication app)
    {
        var startupLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger(StartupLoggerName);

        app.Lifetime.ApplicationStarted.Register(() =>
        {
            var serverAddresses = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>();
            var addresses = serverAddresses?.Addresses ?? app.Urls;

            foreach (var address in addresses.OrderBy(x => x))
            {
                startupLogger.LogInformation(
                    LogTemplate.End,
                    StartupLoggerName,
                    $"Aplicacao iniciada em: {address}");

                if (app.Environment.IsDevelopment())
                {
                    startupLogger.LogInformation(
                        LogTemplate.End,
                        StartupLoggerName,
                        $"Swagger disponivel em: {address.TrimEnd('/')}/swagger/index.html");
                }
            }
        });

        return app;
    }
}
