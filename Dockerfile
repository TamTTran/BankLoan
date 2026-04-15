# ──────────────────────────────────────────────────────────────────────────────
# Stage 1 — Restore & Build  (.NET 8 API only)
# Frontend is built separately in frontend/Dockerfile → served by Nginx
# ──────────────────────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# --- Layer cache: restore only when project files change ---
COPY LoanFlow.sln ./
COPY src/LoanFlow.Domain/LoanFlow.Domain.csproj             src/LoanFlow.Domain/
COPY src/LoanFlow.Application/LoanFlow.Application.csproj   src/LoanFlow.Application/
COPY src/LoanFlow.Infrastructure/LoanFlow.Infrastructure.csproj src/LoanFlow.Infrastructure/
COPY src/LoanFlow.API/LoanFlow.API.csproj                   src/LoanFlow.API/

RUN dotnet restore src/LoanFlow.API/LoanFlow.API.csproj

# --- Full source copy (after restore cache) ---
COPY src/ src/

RUN dotnet publish src/LoanFlow.API/LoanFlow.API.csproj \
    -c Release \
    --no-restore \
    -o /app/publish

# ──────────────────────────────────────────────────────────────────────────────
# Stage 2 — Minimal runtime image
# ──────────────────────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# curl is used by the docker-compose healthcheck
RUN apt-get update && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

# Non-root user for security
RUN addgroup --system appgroup \
    && adduser --system --ingroup appgroup --no-create-home appuser

COPY --from=build /app/publish .

RUN chown -R appuser:appgroup /app
USER appuser

EXPOSE 8080

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_RUNNING_IN_CONTAINER=true

ENTRYPOINT ["dotnet", "LoanFlow.API.dll"]
