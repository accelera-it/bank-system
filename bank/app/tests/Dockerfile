# Build context: bank (see docker-compose.test.yml)
FROM mcr.microsoft.com/dotnet/sdk:10.0
WORKDIR /src

COPY . .
RUN dotnet restore BankSystem.sln
RUN dotnet build BankSystem.sln --no-restore

ENTRYPOINT ["dotnet", "test", "--no-build", "--logger", "console;verbosity=minimal"]
CMD ["BankSystem.sln"]
