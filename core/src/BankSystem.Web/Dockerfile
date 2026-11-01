# Build context: core (see docker-compose.yml)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props ./
COPY src/BankSystem.Common/BankSystem.Common.csproj src/BankSystem.Common/
COPY src/BankSystem.Models/BankSystem.Models.csproj src/BankSystem.Models/
COPY src/BankSystem.Data/BankSystem.Data.csproj src/BankSystem.Data/
COPY src/BankSystem.Services.Models/BankSystem.Services.Models.csproj src/BankSystem.Services.Models/
COPY src/BankSystem.Services/BankSystem.Services.csproj src/BankSystem.Services/
COPY src/BankSystem.Web/BankSystem.Web.csproj src/BankSystem.Web/
RUN dotnet restore src/BankSystem.Web/BankSystem.Web.csproj

COPY src/BankSystem.Common/ src/BankSystem.Common/
COPY src/BankSystem.Models/ src/BankSystem.Models/
COPY src/BankSystem.Data/ src/BankSystem.Data/
COPY src/BankSystem.Services.Models/ src/BankSystem.Services.Models/
COPY src/BankSystem.Services/ src/BankSystem.Services/
COPY src/BankSystem.Web/ src/BankSystem.Web/
RUN dotnet publish src/BankSystem.Web/BankSystem.Web.csproj -c Debug -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:5000 \
    ASPNETCORE_ENVIRONMENT=Development \
    TZ=Asia/Tashkent
EXPOSE 5000
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "BankSystem.Web.dll"]
