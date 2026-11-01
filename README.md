# BankSystem

**BankSystem** is a web banking application built with ASP.NET Core. Users can open bank accounts, transfer money, issue payment cards and pay online with a card or directly from their account.

Several independent BankSystem instances can be connected through **CentralApi**, which securely routes transfers and payments between banks.

## How it works

```
browser ──► nginx :8081 ──► web (BankSystem.Web)
                 │
                 └─ /api ──► ocelot ──► api (CentralApi) ──► web, other BankSystem instances
                                ▲
                  shop (shop-api)
```

All traffic between components is encrypted and signed (RSA + AES hybrid encryption).

## Repository structure

| Folder | Contents | Docs
| - | - | -
| `kyc/` | Keycloak, the central identity system, on PostgreSQL behind nginx | [kyc/README.md](kyc/README.md)
| `bank/` | BankSystem.Web and CentralApi on Oracle and Redis, behind nginx and Ocelot | [bank/README.md](bank/README.md)
| `shop/` | DemoShop: FastAPI and a React shop with an Ant Design cabinet on PostgreSQL and Redis | [shop/README.md](shop/README.md)

Each has its own `data/`, compose files and env file. The .NET projects are in `bank/BankSystem.sln`.

## Quick start

Start `bank` first, because it creates the network the shop joins:

```bash
docker compose -f bank/docker-compose.yml up -d --build
docker compose -f shop/docker-compose.yml up -d --build
```

| Service | Address
|-|-
| **Keycloak** | <http://localhost:8080>
| **BankSystem.Web** | <http://localhost:8081>
| **CentralApi** | <http://localhost:8081/api>
| **Shop** | <http://localhost:8082>
| **Shop cabinet** | <http://localhost:8082/cabinet>

All containers use the `Asia/Tashkent` timezone.

To build and test the .NET projects (no database needed), with the .NET 10 SDK:

```bash
cd bank
dotnet build
dotnet test
```

## Tech stack

- C#, ASP.NET Core 10 (MVC, Razor Pages, Web API)
- Entity Framework Core, Oracle
- Python, FastAPI, SQLAlchemy, PostgreSQL, MinIO
- React, TypeScript, Vite, Tailwind CSS, Ant Design
- Ocelot, nginx
- Redis (login sessions, cache)
- ASP.NET Core Identity
- Mapster
- jQuery, Bootstrap
- xUnit, Moq, FluentAssertions, pytest, Playwright
- Docker, Docker Compose

## Acknowledgements

Based on [banksystembg/BankSystem](https://github.com/banksystembg/BankSystem).
