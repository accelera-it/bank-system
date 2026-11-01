# Build context: repository root (see core/docker-compose.yml)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props ./
COPY core/src/BankSystem.Common/BankSystem.Common.csproj core/src/BankSystem.Common/
COPY core/src/BankSystem.Models/BankSystem.Models.csproj core/src/BankSystem.Models/
COPY core/src/BankSystem.Data/BankSystem.Data.csproj core/src/BankSystem.Data/
COPY core/src/BankSystem.Services.Models/BankSystem.Services.Models.csproj core/src/BankSystem.Services.Models/
COPY core/src/BankSystem.Services/BankSystem.Services.csproj core/src/BankSystem.Services/
COPY core/src/BankSystem.Web/BankSystem.Web.csproj core/src/BankSystem.Web/
RUN dotnet restore core/src/BankSystem.Web/BankSystem.Web.csproj

COPY core/src/BankSystem.Common/ core/src/BankSystem.Common/
COPY core/src/BankSystem.Models/ core/src/BankSystem.Models/
COPY core/src/BankSystem.Data/ core/src/BankSystem.Data/
COPY core/src/BankSystem.Services.Models/ core/src/BankSystem.Services.Models/
COPY core/src/BankSystem.Services/ core/src/BankSystem.Services/
COPY core/src/BankSystem.Web/ core/src/BankSystem.Web/
RUN dotnet publish core/src/BankSystem.Web/BankSystem.Web.csproj -c Debug -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:5000 \
    ASPNETCORE_ENVIRONMENT=Development \
    TZ=Asia/Tashkent
EXPOSE 5000
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "BankSystem.Web.dll"]
