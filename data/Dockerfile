FROM mcr.microsoft.com/mssql/server:2022-latest

ENV ACCEPT_EULA=Y \
    TZ=Asia/Tashkent

EXPOSE 1433

HEALTHCHECK --interval=10s --timeout=5s --start-period=20s --retries=10 \
    CMD /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C -Q "SELECT 1" || exit 1
