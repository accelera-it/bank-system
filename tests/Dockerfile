# Build context: repository root (see docker-compose.test.yml)
FROM mcr.microsoft.com/dotnet/sdk:10.0
WORKDIR /src

COPY . .
RUN dotnet restore BankSystem.sln
RUN dotnet build BankSystem.sln -c Release --no-restore

ENTRYPOINT ["dotnet", "test", "-c", "Release", "--no-build", "--logger", "console;verbosity=minimal"]
CMD ["BankSystem.sln"]
