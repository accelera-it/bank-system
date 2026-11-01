# Build context: core (see docker-compose.yml)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props ./
COPY app/BankSystem.Common/BankSystem.Common.csproj app/BankSystem.Common/
COPY app/CentralApi.Models/CentralApi.Models.csproj app/CentralApi.Models/
COPY app/CentralApi.Data/CentralApi.Data.csproj app/CentralApi.Data/
COPY app/CentralApi.Services.Models/CentralApi.Services.Models.csproj app/CentralApi.Services.Models/
COPY app/CentralApi.Services/CentralApi.Services.csproj app/CentralApi.Services/
COPY app/CentralApi/CentralApi.csproj app/CentralApi/
RUN dotnet restore app/CentralApi/CentralApi.csproj

COPY app/BankSystem.Common/ app/BankSystem.Common/
COPY app/CentralApi.Models/ app/CentralApi.Models/
COPY app/CentralApi.Data/ app/CentralApi.Data/
COPY app/CentralApi.Services.Models/ app/CentralApi.Services.Models/
COPY app/CentralApi.Services/ app/CentralApi.Services/
COPY app/CentralApi/ app/CentralApi/
RUN dotnet publish app/CentralApi/CentralApi.csproj -c Debug -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:5000 \
    ASPNETCORE_ENVIRONMENT=Development \
    TZ=Asia/Tashkent
EXPOSE 5000
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "CentralApi.dll"]
