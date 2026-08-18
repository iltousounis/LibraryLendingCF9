# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first so package changes don't invalidate the source-copy layer.
COPY LendingLibrary.slnx ./
COPY src/LendingLibrary.Web/LendingLibrary.Web.csproj src/LendingLibrary.Web/
RUN dotnet restore src/LendingLibrary.Web/LendingLibrary.Web.csproj

COPY src/LendingLibrary.Web/ src/LendingLibrary.Web/

# Standalone Tailwind CLI (Linux x64) — no Node in the image.
ADD https://github.com/tailwindlabs/tailwindcss/releases/latest/download/tailwindcss-linux-x64 /usr/local/bin/tailwindcss
RUN chmod +x /usr/local/bin/tailwindcss

WORKDIR /src/src/LendingLibrary.Web
RUN dotnet publish LendingLibrary.Web.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# The base image ships a non-root "app" user (uid $APP_UID) — no adduser needed.
# /keys is where the Data Protection key ring volume gets mounted; it must be
# owned by that user before we drop root.
RUN mkdir -p /keys && chown $APP_UID:$APP_UID /keys
USER $APP_UID

COPY --from=build --chown=$APP_UID:$APP_UID /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "LendingLibrary.Web.dll"]
