# syntax=docker/dockerfile:1

# ---------------------------------------------------------------------------
# Build
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

# Restore against the project files alone, so a change to source code does not
# invalidate the restore layer.
COPY src/Blueline.Core/Blueline.Core.csproj      src/Blueline.Core/
COPY src/Blueline.Data/Blueline.Data.csproj      src/Blueline.Data/
COPY src/Blueline.Ingestion/Blueline.Ingestion.csproj src/Blueline.Ingestion/
COPY src/Blueline.Web/Blueline.Web.csproj        src/Blueline.Web/
COPY src/Blueline.Cli/Blueline.Cli.csproj        src/Blueline.Cli/
RUN dotnet restore src/Blueline.Web/Blueline.Web.csproj \
 && dotnet restore src/Blueline.Cli/Blueline.Cli.csproj

# Whatever season archives are present. They are release assets rather than repository
# contents, so run scripts/fetch-seasons.ps1 before building to bake them in. With none
# present the image still builds and the app ingests a season on first run instead.
COPY seed/ seed/
COPY src/ src/

# The site and the loader are published side by side into one directory, so a
# single image can run either. That is what lets an operator seed, reconcile or
# inspect the very same volume the site is using, without a second image and
# without a shell full of dotnet SDK.
RUN dotnet publish src/Blueline.Web/Blueline.Web.csproj -c Release -o /app --no-restore \
 && dotnet publish src/Blueline.Cli/Blueline.Cli.csproj -c Release -o /app --no-restore

# ---------------------------------------------------------------------------
# Runtime
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

# curl is only here so the container can answer its own HEALTHCHECK; the base
# image ships no HTTP client. Hosts that probe from outside do not need it.
RUN apt-get update \
 && apt-get install -y --no-install-recommends curl \
 && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /app ./

# The database lives outside the image so it survives a redeploy. Created and
# owned here so a fresh named volume inherits the right ownership; a bind mount
# takes its permissions from the host and may need chowning there.
ENV BLUELINE_DATA_DIR=/data
RUN mkdir -p /data && chown -R app:app /data
VOLUME ["/data"]

USER app

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

# Deliberately liveness, not readiness. A first run spends several minutes
# loading a season, during which readiness correctly reports "not yet" — probing
# that here would kill the container mid-load and start it over, forever.
HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
    CMD curl --fail --silent http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "Blueline.Web.dll"]
