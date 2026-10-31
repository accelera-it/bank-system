# Build context: repository root (see docker-compose.yml)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props ./
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

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:80 \
    TZ=Asia/Tashkent
EXPOSE 80
COPY --from=build /app/publish .
HEALTHCHECK --interval=10s --timeout=5s --start-period=30s --retries=5 \
    CMD ["bash", "-c", "exec 3<>/dev/tcp/127.0.0.1/80 && printf 'GET /health HTTP/1.0\\r\\nHost: localhost\\r\\n\\r\\n' >&3 && grep -q ' 200 ' <&3"]
ENTRYPOINT ["dotnet", "BankSystem.Web.dll"]
