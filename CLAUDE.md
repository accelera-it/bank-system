# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview

BankSystem is three independently deployed stacks: the bank and its hub, a sample shop and a central sign-in. The first two apps below are an ASP.NET Core (.NET 10) solution (`bank/BankSystem.sln`):

- **BankSystem.Web** (`bank/app/src/BankSystem.Web`) — the bank: MVC + Razor Pages, feature areas `Cards`, `MoneyTransfers`, `Administration`.
- **CentralApi** (`bank/app/src/CentralApi`) — a hub that connects multiple BankSystem instances and routes global transfers, card payments and direct payments between them.
- **DemoShop** (`shop/`) — sample merchant that pays via CentralApi (direct and card payments): Python FastAPI middleware (`shop/api`), and one React + TypeScript + Vite app (`shop/app`): the Tailwind CDN shop site and, under `/cabinet`, the Ant Design cabinet. It is not part of `BankSystem.sln`.
- **Keycloak** (`kyc/`) — the central sign-in for the bank and the shop, on H2, with a provider in `kyc/app/provider` and the `abs` theme.

In `bank`, `bank/proxy` (nginx) is the only public entry (:8081): `/api/` goes to `bank/gateway` (Kong, DB-less, routes in `kong.yml`), everything else to BankSystem.Web. Kong takes every call to CentralApi, including those from the bank and the shop, and CentralApi reaches the bank's API through it at `/bank/api/*`, which nginx does not publish. nginx rate limits `/api/` per client IP (`limit_req` in `nginx.conf`), so internal calls, which never pass through nginx, are not limited. `/pay` exists in both apps, so CentralApi's page is public as `/api/pay` and nginx sends `X-Forwarded-Prefix` so its links keep the prefix (Kong sets that header before its plugins run).

Repo layout: `kyc/` holds Keycloak on H2 (`app/` with Java tests in `app/tests`, `proxy/`). `bank/` holds the bank and CentralApi (`app/{src,core,tests}`, `proxy/` with nginx for the web app, `gateway/` with Kong, `data/`), `shop/` holds DemoShop (`api/` with its package in `src/`, `app/`, `data/`). `data/` has one Dockerfile per backing service, and each side has its own compose files and env file; there is no compose file at the root. Shop tests sit next to their code: `shop/api/tests` (pytest) and `shop/app/tests` (Playwright in TypeScript). `README.md` is the overview; `kyc/README.md`, `bank/README.md` and `shop/README.md` cover setup, tests and configuration for each side, and `docs/` holds the architecture diagram, the technical overview and the slide. `docs/rsa-keygen.sh [bits]` (openssl) generates RSA key pairs in the .NET `<RSAKeyValue>` XML format used in config.

## Commands

Run the .NET commands from `bank/`, which holds `BankSystem.sln`, `Directory.Build.props` (all projects target `net10.0`), `Directory.Packages.props` (central package management) and `dotnet-tools.json` (`dotnet tool restore` installs `dotnet-ef`).

```bash
dotnet restore
dotnet build --no-restore
dotnet test                                                        # all four test projects, no database needed
dotnet test app/tests/BankSystem.UnitTests                        # one project
dotnet test --filter "FullyQualifiedName~MoneyTransferServiceTests" # one class
dotnet test --filter "FullyQualifiedName~MoneyTransferServiceTests.MethodName" # one test
dotnet run --project app/src/BankSystem.Web
dotnet run --project app/src/CentralApi
```

EF Core migrations (SQL Server for BankSystem and CentralApi, one database each; each app calls `Database.Migrate()` on startup):

```bash
dotnet ef migrations add <Name> --project app/src/BankSystem.Data --startup-project app/src/BankSystem.Web
dotnet ef migrations add <Name> --project app/src/CentralApi.Data --startup-project app/src/CentralApi
```

Docker (each app has its own `Dockerfile`; the bank build context is `bank/`). Start kyc, then bank, then shop: each joins the networks of those before it (`kyc_lan`, `bank_lan`). Service names carry the stack prefix (`bank_web`, `shop_api`, `kyc_app`), and MinIO and Keycloak are reached through the `shop-minio` and `kyc-proxy` network aliases, because they reject "_" in a host name.

```bash
docker compose -f kyc/docker-compose.yml up -d --build    # Keycloak :8080
docker compose -f bank/docker-compose.yml up -d --build   # nginx :8081: /api -> bank_gateway (Kong) -> bank_api, the rest -> bank_web (everything else internal only)
docker compose -f shop/docker-compose.yml up -d --build   # ui :8082 with the cabinet at /cabinet (the rest internal only)
docker compose -f bank/docker-compose.test.yml up --build --abort-on-container-failure   # four bank test projects on EF Core InMemory
docker compose -f shop/docker-compose.test.yml up --build --abort-on-container-failure --exit-code-from shop_ui_tests shop_ui_tests   # Playwright, full stack with real Keycloak and a CentralApi stub
docker compose -f kyc/docker-compose.test.yml up --build --abort-on-container-failure --exit-code-from kyc_tests kyc_tests   # Java tests
docker compose -f bank/docker-compose.test.yml run --rm --build bank_api_unit   # one project: bank_{web,api}_{unit,integration}; shop_api_tests (pytest) in shop/
```

Every service has a `healthcheck` in its compose file (not in the Dockerfiles): the apps answer `GET /health` (liveness only), and `depends_on` waits for `service_healthy`.

Each stack loads its own env file through `env_file` (`kyc/kyc.env`, `bank/bank.env`, `shop/shop.env`; the client secrets in `kyc/client.env` go to all three), which holds connection strings, inter-service URLs, RSA keys and every other setting; `appsettings.json` only has logging defaults, and `ASPNETCORE_ENVIRONMENT=Development` is set in the Dockerfiles (`Section__Key`: `ConnectionStrings__BankSystem`, `__CentralApi`, `__Redis`; `shop.env` uses plain names such as `DATABASE_URL`). Apps listen on port 5000 inside the containers, the bank's nginx on 80 and Keycloak on 8080. SQL Server runs as `linux/amd64`, under emulation on Apple Silicon.

## Architecture

### Look
The bank, the shop and the Keycloak theme share the light palette of cbu.uz: gold `#ffc700` / `#e4a216` with dark text, deep gold `#8a5a00` for links and cream `#f6f5e7` behind the pages. There is no dark mode. Everything is served locally, with nothing from a CDN: Inter and Font Awesome in `wwwroot/fonts` and `shop/app/public/fonts`, Tailwind in `shop/app/public/js/tailwind.js`, and CentralApi's Bootstrap and fonts in its `wwwroot/static`, which Kong publishes at `/api/static/`.

### Sign-in (kyc)
Keycloak's `abs` realm (`kyc/app/realms/abs-realm.json`) has the clients `bank` and `shop`; their secrets are in `kyc/client.env`. The realm is imported on first start only. Neither app has login or register pages or keeps passwords: `/account/login` and `/account/register` in the bank and `/api/auth/login` in the shop send the browser to Keycloak (`prompt=create` for register), and it comes back to `/signin-oidc` + `/account/callback` and `/api/auth/callback`, which create the local user on first sign-in, by email. Each app keeps the refresh token with its session (`KycTokenRefresher` in the bank, `current_user` in the shop): expired tokens are renewed on the next request, a refused refresh ends the session, and an unreachable Keycloak leaves it alone. Logout ends the Keycloak session too, and the Edit profile and Change password menu items run Keycloak's forms in a popup through the login flow (`kc_action`); the password form asks for the current password, added by the `abs` theme and checked by the `ABS_UPDATE_PASSWORD` required action in `kyc/app/provider`, and neither app has a settings page. A saved form (`kc_action_status=success`) ends the app session and the Keycloak session and lands on `/relogin.html`, which sends the app behind to login; a cancelled form lands on `/updated.html`, which only reloads it. Both apps take the Full Name from Keycloak on each sign-in. A shop payment is accepted only if the bank user's Full Name equals `RECIPIENT_NAME` in `shop.env`, so the default user's name is `ABS Admin`. Browsers use `localhost:8080`; the apps use `kyc-proxy:5000` for tokens, which Keycloak's dynamic backchannel allows.

### Default user
`demo@abs.uz` / `123456` is the default user: Keycloak holds the password, BankSystem.Web seeds it with an account and the Administrator role, and DemoShop seeds it with a laptop product (`shop/api/src/seed.py`).

### Money
Transfers are atomic, a global transfer with no clear answer holds the money and is delivered again, and a bank credits each reference number once; `bank/CLAUDE.md` has the rules. The shop follows such a payment by its reference number, see `shop/CLAUDE.md`.

### Tests
Four xUnit projects (FluentAssertions, Moq) in `bank/app/tests/{BankSystem,CentralApi}.{UnitTests,IntegrationTests}`; the shop has pytest in `shop/api/tests` and Playwright in `shop/app/tests`, and `kyc/app/tests` has JUnit tests against a real Keycloak on H2.
- Unit tests cover services and crypto/payment helpers on an EF Core InMemory DB.
- Integration tests boot the real app with `WebApplicationFactory`, one instance and database per test class. Helpers are linked from `bank/app/tests/Shared`: `TestDatabase` (EF Core InMemory, so the seeders call `EnsureCreated` instead of migrating; real SQL Server is not covered by tests), `TestAuthHandler` (sign in through headers), `StubHttpServer` (loopback server for the next hop, because the apps create their own `HttpClient`) and `SecureRequestBuilder` (the inter-bank protocol).
