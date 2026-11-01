# Build context: repository root (see demo/docker-compose.yml)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props ./
COPY demo/src/DemoShop.Models/DemoShop.Models.csproj demo/src/DemoShop.Models/
COPY demo/src/DemoShop.Data/DemoShop.Data.csproj demo/src/DemoShop.Data/
COPY demo/src/DemoShop.Services.Models/DemoShop.Services.Models.csproj demo/src/DemoShop.Services.Models/
COPY demo/src/DemoShop.Services/DemoShop.Services.csproj demo/src/DemoShop.Services/
COPY demo/src/DemoShop.Web/DemoShop.Web.csproj demo/src/DemoShop.Web/
RUN dotnet restore demo/src/DemoShop.Web/DemoShop.Web.csproj

COPY demo/src/ demo/src/
RUN dotnet publish demo/src/DemoShop.Web/DemoShop.Web.csproj -c Debug -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:5000 \
    ASPNETCORE_ENVIRONMENT=Development \
    TZ=Asia/Tashkent
EXPOSE 5000
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "DemoShop.Web.dll"]
