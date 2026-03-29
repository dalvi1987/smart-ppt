# ─── Build stage ───────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution and project files first (layer caching)
COPY SmartPPT.sln .
COPY src/SmartPPT.Domain/SmartPPT.Domain.csproj src/SmartPPT.Domain/
COPY src/SmartPPT.Application/SmartPPT.Application.csproj src/SmartPPT.Application/
COPY src/SmartPPT.Infrastructure/SmartPPT.Infrastructure.csproj src/SmartPPT.Infrastructure/
COPY src/SmartPPT.API/SmartPPT.API.csproj src/SmartPPT.API/

RUN dotnet restore

# Copy all source
COPY . .

RUN dotnet publish src/SmartPPT.API/SmartPPT.API.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# ─── Runtime stage ─────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Create storage directory
RUN mkdir -p /app/storage && chmod 777 /app/storage

COPY --from=build /app/publish .

EXPOSE 8080

ENTRYPOINT ["dotnet", "SmartPPT.API.dll"]
