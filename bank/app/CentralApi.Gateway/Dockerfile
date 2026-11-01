# Build context: core (see docker-compose.yml)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props ./
COPY app/CentralApi.Gateway/CentralApi.Gateway.csproj app/CentralApi.Gateway/
RUN dotnet restore app/CentralApi.Gateway/CentralApi.Gateway.csproj

COPY app/CentralApi.Gateway/ app/CentralApi.Gateway/
RUN dotnet publish app/CentralApi.Gateway/CentralApi.Gateway.csproj -c Debug -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:5000 \
    ASPNETCORE_ENVIRONMENT=Development \
    TZ=Asia/Tashkent
EXPOSE 5000
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "CentralApi.Gateway.dll"]
