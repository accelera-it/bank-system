# Build context: repository root (see docker-compose.yml)
FROM mcr.microsoft.com/dotnet/sdk:3.1 AS build
WORKDIR /src

COPY demo/DemoShop.Models/DemoShop.Models.csproj demo/DemoShop.Models/
COPY demo/DemoShop.Data/DemoShop.Data.csproj demo/DemoShop.Data/
COPY demo/DemoShop.Services.Models/DemoShop.Services.Models.csproj demo/DemoShop.Services.Models/
COPY demo/DemoShop.Services/DemoShop.Services.csproj demo/DemoShop.Services/
COPY demo/DemoShop.Web/DemoShop.Web.csproj demo/DemoShop.Web/
RUN dotnet restore demo/DemoShop.Web/DemoShop.Web.csproj

COPY demo/ demo/
RUN dotnet publish demo/DemoShop.Web/DemoShop.Web.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:3.1 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:80 \
    TZ=Asia/Tashkent
EXPOSE 80
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "DemoShop.Web.dll"]
