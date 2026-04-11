# ──────────────────────────────────────────────────────────────────────────────
# Stage 1 — Restore & Build
# ──────────────────────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution + project files first (layer cache for restore)
COPY LoanFlow.sln ./
COPY src/LoanFlow.Domain/LoanFlow.Domain.csproj             src/LoanFlow.Domain/
COPY src/LoanFlow.Application/LoanFlow.Application.csproj   src/LoanFlow.Application/
COPY src/LoanFlow.Infrastructure/LoanFlow.Infrastructure.csproj src/LoanFlow.Infrastructure/
COPY src/LoanFlow.API/LoanFlow.API.csproj                   src/LoanFlow.API/

# Copy all source code
COPY src/ src/

# Restore + Publish in one step (avoids NuGet asset cache hash mismatch)
RUN dotnet publish src/LoanFlow.API/LoanFlow.API.csproj \
    -c Release \
    -o /app/publish

# ──────────────────────────────────────────────────────────────────────────────
# Stage 2 — Runtime image
# ──────────────────────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Install curl for healthcheck
RUN apt-get update && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

# Copy published output
COPY --from=build /app/publish .

# Copy frontend static files (served from wwwroot or a separate Nginx container)
COPY frontend/ ./frontend/

EXPOSE 8080

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "LoanFlow.API.dll"]
