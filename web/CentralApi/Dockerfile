# Build context: repository root (see docker-compose.yml)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props ./
COPY core/BankSystem.Common/BankSystem.Common.csproj core/BankSystem.Common/
COPY core/CentralApi.Models/CentralApi.Models.csproj core/CentralApi.Models/
COPY core/CentralApi.Data/CentralApi.Data.csproj core/CentralApi.Data/
COPY core/CentralApi.Services.Models/CentralApi.Services.Models.csproj core/CentralApi.Services.Models/
COPY core/CentralApi.Services/CentralApi.Services.csproj core/CentralApi.Services/
COPY web/CentralApi/CentralApi.csproj web/CentralApi/
RUN dotnet restore web/CentralApi/CentralApi.csproj

COPY core/BankSystem.Common/ core/BankSystem.Common/
COPY core/CentralApi.Models/ core/CentralApi.Models/
COPY core/CentralApi.Data/ core/CentralApi.Data/
COPY core/CentralApi.Services.Models/ core/CentralApi.Services.Models/
COPY core/CentralApi.Services/ core/CentralApi.Services/
COPY web/CentralApi/ web/CentralApi/
RUN dotnet publish web/CentralApi/CentralApi.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:80 \
    TZ=Asia/Tashkent
EXPOSE 80
COPY --from=build /app/publish .
HEALTHCHECK --interval=10s --timeout=5s --start-period=30s --retries=5 \
    CMD ["bash", "-c", "exec 3<>/dev/tcp/127.0.0.1/80 && printf 'GET /health HTTP/1.0\\r\\nHost: localhost\\r\\n\\r\\n' >&3 && grep -q ' 200 ' <&3"]
ENTRYPOINT ["dotnet", "CentralApi.dll"]
