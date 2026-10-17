using Microsoft.OpenApi.Models;

namespace Fiap.TechChallenge.OficinaMecanica.Billing.API.Bootstrap;

public static class ApiSwaggerBootstrap
{
    public static IServiceCollection AddApiSwagger(this IServiceCollection services)
    {
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "API - Billing Service da Oficina Mecanica",
                Version = "v1",
                Description = "Orcamento e pagamento da oficina mecanica, com integracao ao Mercado Pago",
                Contact = new OpenApiContact
                {
                    Name = "Oficina Mecanica",
                    Email = "contato@oficina.com"
                }
            });

            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "Bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Informe apenas o Token"
            });

            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    },
                    Array.Empty<string>()
                }
            });
        });

        return services;
    }

    public static WebApplication UseApiSwagger(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment())
        {
            return app;
        }

        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "API v1");
            options.RoutePrefix = "swagger";
        });

        return app;
    }
}
