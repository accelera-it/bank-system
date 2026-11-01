# Build context: core (see docker-compose.yml)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props ./
COPY src/BankSystem.Common/BankSystem.Common.csproj src/BankSystem.Common/
COPY src/CentralApi.Models/CentralApi.Models.csproj src/CentralApi.Models/
COPY src/CentralApi.Data/CentralApi.Data.csproj src/CentralApi.Data/
COPY src/CentralApi.Services.Models/CentralApi.Services.Models.csproj src/CentralApi.Services.Models/
COPY src/CentralApi.Services/CentralApi.Services.csproj src/CentralApi.Services/
COPY src/CentralApi/CentralApi.csproj src/CentralApi/
RUN dotnet restore src/CentralApi/CentralApi.csproj

COPY src/BankSystem.Common/ src/BankSystem.Common/
COPY src/CentralApi.Models/ src/CentralApi.Models/
COPY src/CentralApi.Data/ src/CentralApi.Data/
COPY src/CentralApi.Services.Models/ src/CentralApi.Services.Models/
COPY src/CentralApi.Services/ src/CentralApi.Services/
COPY src/CentralApi/ src/CentralApi/
RUN dotnet publish src/CentralApi/CentralApi.csproj -c Debug -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:5000 \
    ASPNETCORE_ENVIRONMENT=Development \
    TZ=Asia/Tashkent
EXPOSE 5000
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "CentralApi.dll"]
