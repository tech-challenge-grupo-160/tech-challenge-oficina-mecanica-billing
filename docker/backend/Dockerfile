FROM mcr.microsoft.com/dotnet/sdk:10.0 AS development
WORKDIR /app

# copy csproj and restore
COPY ["src/API/Fiap.TechChallenge.OficinaMecanica.Billing.Api.csproj", "src/API/"]
COPY ["src/Application/Fiap.TechChallenge.OficinaMecanica.Billing.Application.csproj", "src/Application/"]
COPY ["src/Domain/Fiap.TechChallenge.OficinaMecanica.Billing.Domain.csproj", "src/Domain/"]
COPY ["src/Infrastructure/Fiap.TechChallenge.OficinaMecanica.Billing.Infrastructure.csproj", "src/Infrastructure/"]
RUN dotnet restore "src/API/Fiap.TechChallenge.OficinaMecanica.Billing.Api.csproj"

# copy everything
COPY . .

# Enable hot reload
ENV DOTNET_WATCH_RESTART_ON_RUDE_EDIT=true

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Development

ENTRYPOINT ["dotnet", "watch", "run", "--project", "src/API/Fiap.TechChallenge.OficinaMecanica.Billing.Api.csproj", "--urls", "http://+:8080"]


FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# copy csproj and restore
COPY ["src/API/Fiap.TechChallenge.OficinaMecanica.Billing.Api.csproj", "src/API/"]
COPY ["src/Application/Fiap.TechChallenge.OficinaMecanica.Billing.Application.csproj", "src/Application/"]
COPY ["src/Domain/Fiap.TechChallenge.OficinaMecanica.Billing.Domain.csproj", "src/Domain/"]
COPY ["src/Infrastructure/Fiap.TechChallenge.OficinaMecanica.Billing.Infrastructure.csproj", "src/Infrastructure/"]
RUN dotnet restore "src/API/Fiap.TechChallenge.OficinaMecanica.Billing.Api.csproj"

# copy everything and publish
COPY . .
RUN dotnet publish "src/API/Fiap.TechChallenge.OficinaMecanica.Billing.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM alpine:3.22 AS datadog-tracer
ARG DD_TRACE_VERSION=3.53.0
RUN apk add --no-cache curl tar \
    && mkdir -p /opt/datadog \
    && curl --fail --silent --show-error --location \
        "https://github.com/DataDog/dd-trace-dotnet/releases/download/v${DD_TRACE_VERSION}/datadog-dotnet-apm-${DD_TRACE_VERSION}-musl.tar.gz" \
        | tar -xzf - -C /opt/datadog \
    && mkdir -p /var/log/datadog/dotnet

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime
WORKDIR /app

# O Datadog Tracer (build musl) carrega libgssapi_krb5.so.2 em tempo de
# execucao; a imagem alpine base do .NET nao inclui krb5.
RUN apk add --no-cache krb5-libs

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV CORECLR_ENABLE_PROFILING=1
ENV CORECLR_PROFILER={846F5F1C-F9AE-4B07-969E-05C26BC060D8}
ENV CORECLR_PROFILER_PATH=/opt/datadog/Datadog.Trace.ClrProfiler.Native.so
ENV DD_DOTNET_TRACER_HOME=/opt/datadog
ENV DD_TRACE_ENABLED=true
ENV DD_SERVICE=oficina-mecanica-billing
ENV DD_ENV=Development
ENV DD_VERSION=unknown

COPY --from=datadog-tracer /opt/datadog /opt/datadog
COPY --from=datadog-tracer /var/log/datadog/dotnet /var/log/datadog/dotnet
COPY --from=build --chown=app:app /app/publish .

RUN chown -R app:app /opt/datadog /var/log/datadog
USER app
ENTRYPOINT ["dotnet", "Fiap.TechChallenge.OficinaMecanica.Billing.Api.dll"]
