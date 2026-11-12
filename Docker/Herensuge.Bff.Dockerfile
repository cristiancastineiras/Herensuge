# syntax=docker/dockerfile:1
# Herensuge.Bff (portal backend) + portal web. Build from the repository root:
#   docker build -f Docker/Herensuge.Bff.Dockerfile -t herensuge-bff .

# 1. Portal web (Src/Web/apps/portal) → /web/apps/portal/dist
FROM node:24-slim AS web
# Public URL of the IdP, used to link Google/Microsoft accounts (empty = not offered).
ARG VITE_URL_IDP=""
ENV VITE_URL_IDP=${VITE_URL_IDP}
RUN npm install --global --no-fund --no-audit pnpm@10.32.1
WORKDIR /web
# Only the lockfile first: the package download layer is reused until a dependency changes.
COPY Src/Web/pnpm-lock.yaml Src/Web/pnpm-workspace.yaml Src/Web/.npmrc ./
RUN pnpm fetch
COPY Src/Web/ ./
RUN pnpm install --offline --frozen-lockfile && pnpm --filter portal build

# 2. .NET
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json nuget.config Directory.Build.props Directory.Packages.props ./
COPY Src/Hosts/Herensuge.Bff/ Src/Hosts/Herensuge.Bff/
RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages \
    dotnet restore Src/Hosts/Herensuge.Bff/Herensuge.Bff.csproj
RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages \
    dotnet publish Src/Hosts/Herensuge.Bff/Herensuge.Bff.csproj -c Release -o /app --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled-extra AS runtime
WORKDIR /app
COPY --from=build /app .
COPY --from=web /web/apps/portal/dist ./wwwroot
ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_gcServer=1
EXPOSE 8080
USER 1654
ENTRYPOINT ["dotnet", "Herensuge.Bff.dll"]
