# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copia apenas os .csproj para aproveitar o cache de restore.
COPY src/TASKO.Domain/TASKO.Domain.csproj src/TASKO.Domain/
COPY src/TASKO.Application/TASKO.Application.csproj src/TASKO.Application/
COPY src/TASKO.Infrastructure/TASKO.Infrastructure.csproj src/TASKO.Infrastructure/
COPY src/TASKO.Api/TASKO.Api.csproj src/TASKO.Api/
RUN dotnet restore src/TASKO.Api/TASKO.Api.csproj

COPY src/ src/
RUN dotnet publish src/TASKO.Api/TASKO.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_NOLOGO=1 \
    DOTNET_CLI_TELEMETRY_OPTOUT=1

COPY --from=build /app/publish .

EXPOSE 8080
USER $APP_UID

ENTRYPOINT ["dotnet", "TASKO.Api.dll"]
