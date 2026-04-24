# syntax=docker/dockerfile:1.7

# ── Build stage ───────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first for layer caching: copy only the project files, restore, then copy the rest.
COPY dwa_ver_val.csproj ./
COPY Tests/dwa_ver_val.Tests.csproj ./Tests/
RUN dotnet restore dwa_ver_val.csproj

# Copy the rest of the source.
COPY . .

RUN dotnet publish dwa_ver_val.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

# ── Runtime stage ────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# QuestPDF on Linux needs fontconfig + common fonts for PDF text rendering.
RUN apt-get update \
    && apt-get install -y --no-install-recommends libfontconfig1 fonts-dejavu-core \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

# Railway (and similar PaaS) inject PORT at runtime. Kestrel must bind to 0.0.0.0:$PORT.
# Default to 8080 when PORT isn't set (local docker run).
ENV ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true \
    ASPNETCORE_URLS=http://+:${PORT:-8080}

# Documenting the default port; actual binding comes from ASPNETCORE_URLS + PORT.
EXPOSE 8080

ENTRYPOINT ["dotnet", "dwa_ver_val.dll"]
