# Immagine dell'API, che ospita anche i job di import. Si costruisce dalla radice del repo:
#   docker build -f src/SunCash.Api/Dockerfile .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/SunCash.Domain/SunCash.Domain.csproj src/SunCash.Domain/
COPY src/SunCash.Infrastructure/SunCash.Infrastructure.csproj src/SunCash.Infrastructure/
COPY src/SunCash.Api/SunCash.Api.csproj src/SunCash.Api/
RUN dotnet restore src/SunCash.Api/SunCash.Api.csproj
COPY src/ src/
RUN dotnet publish src/SunCash.Api/SunCash.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
# Il file SQLite vive nel volume /data, scrivibile dall'utente non privilegiato dell'immagine.
RUN mkdir -p /data && chown "$APP_UID" /data
USER $APP_UID
COPY --from=build /app .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
VOLUME ["/data"]
ENTRYPOINT ["dotnet", "SunCash.Api.dll"]
