# syntax=docker/dockerfile:1
# La BFF de Fichajes: la misma Herensuge.Bff del portal, con la web de Fichajes en wwwroot. La configuración
# (cliente OIDC, scopes, destino de /api) llega por variables de entorno (compose.yaml). Desde la raíz del repositorio:
#   docker build -f FichajesApp/Fichajes.Bff.Dockerfile -t fichajes-bff .

# 1. Web (FichajesApp/web). @herensuge/react se enlaza desde el repositorio (link:); su tsconfig hereda de
#    configuracion-ts.
FROM node:24-slim AS web
RUN npm install --global --no-fund --no-audit pnpm@10.32.1
COPY Src/Web/paquetes/herensuge-react/ /repo/Src/Web/paquetes/herensuge-react/
COPY Src/Web/paquetes/configuracion-ts/ /repo/Src/Web/paquetes/configuracion-ts/
WORKDIR /repo/FichajesApp/web
COPY FichajesApp/web/package.json FichajesApp/web/pnpm-lock.yaml FichajesApp/web/.npmrc ./
RUN pnpm install --frozen-lockfile
COPY FichajesApp/web/ ./
RUN pnpm build

# 2. Herensuge.Bff
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
COPY --from=web /repo/FichajesApp/web/dist ./wwwroot
ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_gcServer=1
EXPOSE 8080
USER 1654
ENTRYPOINT ["dotnet", "Herensuge.Bff.dll"]
