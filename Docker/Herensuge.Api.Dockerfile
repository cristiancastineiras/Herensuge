# syntax=docker/dockerfile:1
# Herensuge.Api (IdP). Build from the repository root:
#   docker build -f Docker/Herensuge.Api.Dockerfile -t herensuge-api .
# When the login SPA exists (Src/Web/apps/login), add a node stage here and copy its dist/ to /app/wwwroot.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json nuget.config Directory.Build.props Directory.Packages.props ./
COPY Src/ Src/
RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages \
    dotnet restore Src/Hosts/Herensuge.Api/Herensuge.Api.csproj
RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages \
    dotnet publish Src/Hosts/Herensuge.Api/Herensuge.Api.csproj -c Release -o /app --no-restore /p:UseAppHost=false
# Writable folder for the OIDC signing/encryption certificates (mounted as a volume).
RUN mkdir -p /keys && chown 1654:1654 /keys

# Distroless Ubuntu image with ICU and time zones; runs as the non-root "app" user (UID 1654).
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled-extra AS runtime
WORKDIR /app
COPY --from=build /app .
COPY --from=build --chown=1654:1654 /keys /keys
ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_gcServer=1
EXPOSE 8080
USER 1654
ENTRYPOINT ["dotnet", "Herensuge.Api.dll"]
