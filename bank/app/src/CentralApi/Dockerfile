# Build context: core (see docker-compose.yml)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props ./
COPY app/core/BankSystem.Common/BankSystem.Common.csproj app/core/BankSystem.Common/
COPY app/src/CentralApi.Models/CentralApi.Models.csproj app/src/CentralApi.Models/
COPY app/src/CentralApi.Data/CentralApi.Data.csproj app/src/CentralApi.Data/
COPY app/src/CentralApi.Services.Models/CentralApi.Services.Models.csproj app/src/CentralApi.Services.Models/
COPY app/src/CentralApi.Services/CentralApi.Services.csproj app/src/CentralApi.Services/
COPY app/src/CentralApi/CentralApi.csproj app/src/CentralApi/
RUN dotnet restore app/src/CentralApi/CentralApi.csproj

COPY app/core/BankSystem.Common/ app/core/BankSystem.Common/
COPY app/src/CentralApi.Models/ app/src/CentralApi.Models/
COPY app/src/CentralApi.Data/ app/src/CentralApi.Data/
COPY app/src/CentralApi.Services.Models/ app/src/CentralApi.Services.Models/
COPY app/src/CentralApi.Services/ app/src/CentralApi.Services/
COPY app/src/CentralApi/ app/src/CentralApi/
RUN dotnet publish app/src/CentralApi/CentralApi.csproj -c Debug -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:5000 \
    ASPNETCORE_ENVIRONMENT=Development \
    TZ=Asia/Tashkent
EXPOSE 5000
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "CentralApi.dll"]
