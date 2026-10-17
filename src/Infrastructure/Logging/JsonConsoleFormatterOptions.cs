using Microsoft.Extensions.Logging.Console;

namespace Fiap.TechChallenge.OficinaMecanica.Billing.Infrastructure.Logging;

public sealed class JsonConsoleFormatterOptions : ConsoleFormatterOptions
{
    public string ServiceName { get; set; } = "oficina-mecanica-api";

    public string EnvironmentName { get; set; } = "Production";
}
