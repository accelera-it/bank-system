# Build context: core (see docker-compose.yml)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props ./
COPY app/BankSystem.Common/BankSystem.Common.csproj app/BankSystem.Common/
COPY app/BankSystem.Models/BankSystem.Models.csproj app/BankSystem.Models/
COPY app/BankSystem.Data/BankSystem.Data.csproj app/BankSystem.Data/
COPY app/BankSystem.Services.Models/BankSystem.Services.Models.csproj app/BankSystem.Services.Models/
COPY app/BankSystem.Services/BankSystem.Services.csproj app/BankSystem.Services/
COPY app/BankSystem.Web/BankSystem.Web.csproj app/BankSystem.Web/
RUN dotnet restore app/BankSystem.Web/BankSystem.Web.csproj

COPY app/BankSystem.Common/ app/BankSystem.Common/
COPY app/BankSystem.Models/ app/BankSystem.Models/
COPY app/BankSystem.Data/ app/BankSystem.Data/
COPY app/BankSystem.Services.Models/ app/BankSystem.Services.Models/
COPY app/BankSystem.Services/ app/BankSystem.Services/
COPY app/BankSystem.Web/ app/BankSystem.Web/
RUN dotnet publish app/BankSystem.Web/BankSystem.Web.csproj -c Debug -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:5000 \
    ASPNETCORE_ENVIRONMENT=Development \
    TZ=Asia/Tashkent
EXPOSE 5000
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "BankSystem.Web.dll"]
