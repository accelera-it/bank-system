# BankSystem

**BankSystem** is a web banking application built with ASP.NET Core, with a sample shop and one central sign-in through Keycloak. Users can open bank accounts, transfer money, issue payment cards and pay online with a card or directly from their account.

Several independent BankSystem instances can be connected through **CentralApi**, which securely routes transfers and payments between banks. A transfer moves once: it is held and delivered again when a call gets no clear answer, and a shop payment follows it to the end.

## How it works

```
browser ──► bank_proxy :8081 ──► bank_web (BankSystem.Web)
                  │
                  └─ /api ──► bank_gateway (Kong) ──► bank_api (CentralApi)
                                   ▲    │
                        shop_api ──┘    └──► bank_web, other BankSystem instances

bank_web ──► bank_rabbit (RabbitMQ) ──► bank_web, shop_api

browser, bank_web, shop_api ──► kyc_proxy :8080 ──► kyc_app (Keycloak)
```

The bank and the shop have no login or register pages: users sign in at Keycloak and come back. All traffic between banks and CentralApi is encrypted and signed (RSA + AES hybrid encryption).

## Repository structure

| Folder | Contents | Docs
| - | - | -
| `kyc/` | Keycloak, the central identity system for the bank and the shop, on Postgres behind nginx | [kyc/README.md](kyc/README.md)
| `bank/` | BankSystem.Web and CentralApi on Oracle, Redis and RabbitMQ, behind nginx and Kong | [bank/README.md](bank/README.md)
| `shop/` | DemoShop: FastAPI and a React shop with an Ant Design cabinet on PostgreSQL and Redis | [shop/README.md](shop/README.md)

Each has its own compose files and env file. The .NET projects are in `bank/BankSystem.sln`. `docs/` holds the [architecture diagram](docs/infra.png), the [technical overview](docs/tech.pdf) and the [slide](docs/slide.pdf).

## Quick start

Start them in this order, because each one joins the networks of those before it:

```bash
docker compose -f kyc/docker-compose.yml up -d --build
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

Sign in with `demo@abs.uz` / `123456`. The Keycloak root user is `admin` / `123456`. All containers use the `Asia/Tashkent` timezone.

To build and test the .NET projects (no database needed), with the .NET 10 SDK:

```bash
cd bank
dotnet build
dotnet test
```

## Tech stack

- C#, ASP.NET Core 10 (MVC, Razor Pages, Web API)
- Entity Framework Core, Oracle
- PostgreSQL (Keycloak)
- Python, FastAPI, SQLAlchemy, PostgreSQL, MinIO
- React, TypeScript, Vite, Tailwind CSS, Ant Design
- Keycloak, Kong, nginx
- Redis (login sessions and cache of the bank and the shop)
- RabbitMQ (pending transfers, settled payments)
- ASP.NET Core Identity, OpenID Connect
- Mapster
- jQuery, Bootstrap
- xUnit, Moq, FluentAssertions, pytest, Playwright, JUnit
- Docker, Docker Compose

## Acknowledgements

Based on [banksystembg/BankSystem](https://github.com/banksystembg/BankSystem).
