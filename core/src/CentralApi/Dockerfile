# Build context: repository root (see core/docker-compose.yml)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props ./
COPY core/src/BankSystem.Common/BankSystem.Common.csproj core/src/BankSystem.Common/
COPY core/src/CentralApi.Models/CentralApi.Models.csproj core/src/CentralApi.Models/
COPY core/src/CentralApi.Data/CentralApi.Data.csproj core/src/CentralApi.Data/
COPY core/src/CentralApi.Services.Models/CentralApi.Services.Models.csproj core/src/CentralApi.Services.Models/
COPY core/src/CentralApi.Services/CentralApi.Services.csproj core/src/CentralApi.Services/
COPY core/src/CentralApi/CentralApi.csproj core/src/CentralApi/
RUN dotnet restore core/src/CentralApi/CentralApi.csproj

COPY core/src/BankSystem.Common/ core/src/BankSystem.Common/
COPY core/src/CentralApi.Models/ core/src/CentralApi.Models/
COPY core/src/CentralApi.Data/ core/src/CentralApi.Data/
COPY core/src/CentralApi.Services.Models/ core/src/CentralApi.Services.Models/
COPY core/src/CentralApi.Services/ core/src/CentralApi.Services/
COPY core/src/CentralApi/ core/src/CentralApi/
RUN dotnet publish core/src/CentralApi/CentralApi.csproj -c Debug -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:5000 \
    ASPNETCORE_ENVIRONMENT=Development \
    TZ=Asia/Tashkent
EXPOSE 5000
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "CentralApi.dll"]
