# syntax=docker/dockerfile:1
# Fichajes.Api. Se construye desde la raíz del repositorio (usa el SDK Herensuge.Access.AspNetCore del repo):
#   docker build -f FichajesApp/Fichajes.Api.Dockerfile -t fichajes-api .

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json nuget.config Directory.Build.props Directory.Packages.props ./
COPY Src/Sdk/Herensuge.Access.AspNetCore/ Src/Sdk/Herensuge.Access.AspNetCore/
COPY FichajesApp/Fichajes.Api/ FichajesApp/Fichajes.Api/
RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages \
    dotnet restore FichajesApp/Fichajes.Api/Fichajes.Api.csproj
RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages \
    dotnet publish FichajesApp/Fichajes.Api/Fichajes.Api.csproj -c Release -o /app --no-restore /p:UseAppHost=false

# Imagen distroless sin shell; corre con el usuario sin privilegios "app" (UID 1654).
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled-extra AS runtime
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER 1654
ENTRYPOINT ["dotnet", "Fichajes.Api.dll"]
