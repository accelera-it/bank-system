# Build context: repository root (see docker-compose.yml)
FROM mcr.microsoft.com/dotnet/sdk:3.1 AS build
WORKDIR /src

COPY core/BankSystem.Common/BankSystem.Common.csproj core/BankSystem.Common/
COPY core/BankSystem.Models/BankSystem.Models.csproj core/BankSystem.Models/
COPY core/BankSystem.Data/BankSystem.Data.csproj core/BankSystem.Data/
COPY core/BankSystem.Services.Models/BankSystem.Services.Models.csproj core/BankSystem.Services.Models/
COPY core/BankSystem.Services/BankSystem.Services.csproj core/BankSystem.Services/
COPY web/BankSystem.Web/BankSystem.Web.csproj web/BankSystem.Web/
RUN dotnet restore web/BankSystem.Web/BankSystem.Web.csproj

COPY core/BankSystem.Common/ core/BankSystem.Common/
COPY core/BankSystem.Models/ core/BankSystem.Models/
COPY core/BankSystem.Data/ core/BankSystem.Data/
COPY core/BankSystem.Services.Models/ core/BankSystem.Services.Models/
COPY core/BankSystem.Services/ core/BankSystem.Services/
COPY web/BankSystem.Web/ web/BankSystem.Web/
RUN dotnet publish web/BankSystem.Web/BankSystem.Web.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:3.1 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:80
EXPOSE 80
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "BankSystem.Web.dll"]
