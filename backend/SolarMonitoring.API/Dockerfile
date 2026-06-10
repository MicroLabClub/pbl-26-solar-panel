# syntax=docker/dockerfile:1

# --- Build stage ---
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restore first (cached unless the csproj changes).
COPY SolarMonitoring.API.csproj ./
RUN dotnet restore SolarMonitoring.API.csproj

# Build + publish.
COPY . ./
RUN dotnet publish SolarMonitoring.API.csproj -c Release -o /app/publish /p:UseAppHost=false

# --- Runtime stage ---
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_URLS=http://+:8080

COPY --from=build /app/publish ./

# Run as the non-root user shipped with the aspnet image.
USER $APP_UID

EXPOSE 8080
ENTRYPOINT ["dotnet", "SolarMonitoring.API.dll"]
