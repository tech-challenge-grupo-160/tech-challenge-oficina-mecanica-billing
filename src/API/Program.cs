using Fiap.TechChallenge.OficinaMecanica.Billing.API.Bootstrap;
using Fiap.TechChallenge.OficinaMecanica.Billing.Application;
using Fiap.TechChallenge.OficinaMecanica.Billing.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.ConfigureApiLogging();

builder.Services.AddApiServices();
builder.Services.AddApplication();
builder.Services.AddInfrastructure();

var app = builder.Build();

app.UseApiPipeline(builder.Configuration);
app.LogStartupUrls();

app.Run();

public partial class Program;
