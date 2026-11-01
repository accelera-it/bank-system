# BankSystem

**BankSystem** is a web banking application built with ASP.NET Core. Users can open bank accounts, transfer money, issue payment cards and pay online with a card or directly from their account.

Several independent BankSystem instances can be connected through **CentralApi**, which securely routes transfers and payments between banks.

## How it works

```
demo (DemoShop) ──► api (CentralApi) ──► web (BankSystem.Web)
                          │
                          └──► other BankSystem instances
```

All traffic between components is encrypted and signed (RSA + AES hybrid encryption).

## Repository structure

| Folder | Contents | Docs
| - | - | -
| `core/` | BankSystem.Web and CentralApi on Oracle and Redis | [core/README.md](core/README.md)
| `demo/` | DemoShop on PostgreSQL and Redis | [demo/README.md](demo/README.md)

Each has its own `src/`, `tests/`, `data/`, compose files and env file. All projects are in `BankSystem.sln` at the repository root.

## Quick start

Start `core` first, because it creates the network the shop joins:

```bash
docker compose -f core/docker-compose.yml up -d --build
docker compose -f demo/docker-compose.yml up -d --build
```

| Service | Address
|-|-
| **BankSystem.Web** | <http://localhost:8081>
| **CentralApi** | <http://localhost:8080>
| **DemoShop** | <http://localhost:8082>

All containers use the `Asia/Tashkent` timezone.

To build and test everything (no database needed), with the .NET 10 SDK:

```bash
dotnet build
dotnet test
```

## Tech stack

- C#, ASP.NET Core 10 (MVC, Razor Pages, Web API)
- Entity Framework Core, Oracle, PostgreSQL
- Redis (login sessions, cache)
- ASP.NET Core Identity
- Mapster
- jQuery, Bootstrap
- xUnit, Moq, FluentAssertions
- Docker, Docker Compose

## Acknowledgements

Based on [banksystembg/BankSystem](https://github.com/banksystembg/BankSystem).
