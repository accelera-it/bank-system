# Build context: core (see docker-compose.yml)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props ./
COPY app/core/BankSystem.Common/BankSystem.Common.csproj app/core/BankSystem.Common/
COPY app/src/BankSystem.Models/BankSystem.Models.csproj app/src/BankSystem.Models/
COPY app/src/BankSystem.Data/BankSystem.Data.csproj app/src/BankSystem.Data/
COPY app/src/BankSystem.Services.Models/BankSystem.Services.Models.csproj app/src/BankSystem.Services.Models/
COPY app/src/BankSystem.Services/BankSystem.Services.csproj app/src/BankSystem.Services/
COPY app/src/BankSystem.Web/BankSystem.Web.csproj app/src/BankSystem.Web/
RUN dotnet restore app/src/BankSystem.Web/BankSystem.Web.csproj

COPY app/core/BankSystem.Common/ app/core/BankSystem.Common/
COPY app/src/BankSystem.Models/ app/src/BankSystem.Models/
COPY app/src/BankSystem.Data/ app/src/BankSystem.Data/
COPY app/src/BankSystem.Services.Models/ app/src/BankSystem.Services.Models/
COPY app/src/BankSystem.Services/ app/src/BankSystem.Services/
COPY app/src/BankSystem.Web/ app/src/BankSystem.Web/
RUN dotnet publish app/src/BankSystem.Web/BankSystem.Web.csproj -c Debug -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:5000 \
    ASPNETCORE_ENVIRONMENT=Development \
    TZ=Asia/Tashkent
EXPOSE 5000
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "BankSystem.Web.dll"]
