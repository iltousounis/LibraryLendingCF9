# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first so package changes don't invalidate the source-copy layer.
COPY LendingLibrary.slnx ./
COPY src/LendingLibrary.Web/LendingLibrary.Web.csproj src/LendingLibrary.Web/
RUN dotnet restore src/LendingLibrary.Web/LendingLibrary.Web.csproj

COPY src/LendingLibrary.Web/ src/LendingLibrary.Web/

# Standalone Tailwind CLI (Linux x64), pinned for reproducible builds — no Node in the image.
ARG TAILWIND_VERSION=v4.3.3
ADD https://github.com/tailwindlabs/tailwindcss/releases/download/${TAILWIND_VERSION}/tailwindcss-linux-x64 /usr/local/bin/tailwindcss
RUN chmod +x /usr/local/bin/tailwindcss

WORKDIR /src/src/LendingLibrary.Web
RUN dotnet publish LendingLibrary.Web.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# curl for the HEALTHCHECK below; installed while still root.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

# The base image ships a non-root "app" user (uid $APP_UID) — no adduser needed.
# /keys is where the Data Protection key ring volume gets mounted; it must be
# owned by that user before we drop root.
RUN mkdir -p /keys && chown $APP_UID:$APP_UID /keys
USER $APP_UID

COPY --from=build --chown=$APP_UID:$APP_UID /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=5s --start-period=15s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "LendingLibrary.Web.dll"]
