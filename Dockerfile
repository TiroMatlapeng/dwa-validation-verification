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

# Create writable upload directories and hand ownership to the non-root runtime user (1000).
# The Helm deployment runs as runAsUser/fsGroup 1000; without this, FileSystemBlobStore's
# Directory.CreateDirectory fails with UnauthorizedAccessException and every request 500s.
# /app/wwwroot/_uploads = letter PDFs; /app/portal-uploads = Helm blobStore.useLocalDisk mount.
RUN mkdir -p /app/wwwroot/_uploads /app/portal-uploads \
    && chown -R 1000:1000 /app/wwwroot/_uploads /app/portal-uploads

# Railway (and similar PaaS) inject PORT at runtime. Kestrel must bind to 0.0.0.0:$PORT.
# Default to 8080 when PORT isn't set (local docker run).
ENV ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true \
    ASPNETCORE_URLS=http://+:${PORT:-8080}

# Documenting the default port; actual binding comes from ASPNETCORE_URLS + PORT.
EXPOSE 8080

# Run as non-root (matches Helm securityContext runAsUser/fsGroup 1000).
USER 1000

ENTRYPOINT ["dotnet", "dwa_ver_val.dll"]
