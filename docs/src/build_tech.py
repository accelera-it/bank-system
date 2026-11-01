"""Writes tech.html next to this file. Run: python3 docs/src/build_tech.py"""

from html import escape
from pathlib import Path


def table(head, rows):
    cells = "".join(f"<th>{escape(cell)}</th>" for cell in head)
    body = "".join("<tr>" + "".join(f"<td>{cell}</td>" for cell in row) + "</tr>" for row in rows)
    return f"<table><thead><tr>{cells}</tr></thead><tbody>{body}</tbody></table>"


def steps(items):
    return "<ol class='steps'>" + "".join(f"<li>{item}</li>" for item in items) + "</ol>"


SECTIONS = [
    ("Goals and Key Properties", "Banks stay independent; the hub and the sign-in are shared.", table(
        ["Property", "How it is ensured"],
        [
            ["One account everywhere", "Keycloak holds every password. The bank and the shop have no login or register pages and create their local user on first sign-in, by email."],
            ["Banks stay independent", "Each bank has its own database, RSA key and identity. CentralApi only routes between them, by name, swift code and country."],
            ["Bank traffic is unreadable and signed", "Every call is hybrid encrypted with AES and an RSA-wrapped key, and signed by the sender. Verification failures answer 403."],
            ["Sessions follow Keycloak", "Each app keeps the refresh token. Expired tokens are renewed; a refused refresh ends the session at once."],
            ["A credential change signs out", "A saved profile or password ends the app session and the Keycloak session, so the user signs in again."],
            ["Small public surface", "Only nginx in each stack publishes a port. Databases, Redis, MinIO, the gateway and the apps are internal."],
            ["Public API calls are limited", "nginx allows each client IP 60 calls a minute on <code>/api/</code>, with short bursts. Internal calls do not pass through it and are not limited."],
            ["Money moves once", "A transfer is saved in one database transaction, and a delivery with no clear answer is repeated until there is one. A bank credits each reference number once."],
            ["Keycloak being away signs nobody out", "A network failure during refresh leaves the session alone, and the next request tries again."],
        ],
    )),
    ("System Context", "Who uses the system and what it depends on.", table(
        ["Actor", "Reaches", "Through"],
        [
            ["Customer", "Bank pages, shop site and cabinet", "bank_proxy :8081, shop_ui :8082"],
            ["Customer signing in", "Login, register, profile and password forms", "kyc_proxy :8080"],
            ["Bank staff", "All accounts, transfer lookup, pending transfers", "Administration area of the bank"],
            ["Merchant (shop)", "Direct and card payments", "bank_gateway (Kong)"],
            ["Other banks", "Global transfers and card payments", "CentralApi, signed and encrypted"],
            ["Keycloak root user", "Realm administration", "kyc_proxy :8080/admin; kyc_app makes it permanent"],
        ],
    )),
    ("Components", "Three stacks, each with its own Compose file and env file.", table(
        ["Stack", "Service", "Technology", "Role"],
        [
            ["bank", "<code>bank_proxy</code>", "nginx", "Public entry: <code>/api/</code> to the gateway, the rest to the bank; rate limit"],
            ["bank", "<code>bank_web</code>", "ASP.NET Core 10", "Accounts, cards, transfers, payments, administration"],
            ["bank", "<code>bank_gateway</code>", "Kong, DB-less", "Gateway for every API call, routes in <code>kong.yml</code>"],
            ["bank", "<code>bank_api</code>", "ASP.NET Core 10", "CentralApi: routes transfers and payments between banks"],
            ["bank", "<code>bank_mssql</code>", "SQL Server 2022", "BankSystem and CentralApi databases"],
            ["bank", "<code>bank_redis</code>", "Redis", "Login tickets, cache, Data Protection keys"],
            ["bank", "<code>bank_rabbit</code>", "RabbitMQ", "Delivery and retry queues for pending transfers, settled payments for the shop"],
            ["shop", "<code>shop_ui</code>", "nginx, React, TypeScript", "Shop site and cabinet, proxies <code>/api/</code>"],
            ["shop", "<code>shop_api</code>", "FastAPI, SQLAlchemy", "Products, orders, payments, OIDC callback"],
            ["shop", "<code>shop_pg</code>", "PostgreSQL", "Users, products, orders"],
            ["shop", "<code>shop_redis</code>", "Redis", "Sessions, tokens, product cache"],
            ["shop", "<code>shop_minio</code>", "MinIO", "Product images in a private bucket"],
            ["kyc", "<code>kyc_proxy</code>", "nginx", "Public entry to Keycloak"],
            ["kyc", "<code>kyc_app</code>", "Keycloak 26, H2", "Sign-in, realm <code>abs</code>, clients <code>bank</code> and <code>shop</code>"],
        ],
    )),
    ("Startup Order and Ports", "Each stack joins the networks of those before it.", table(
        ["Order", "Stack", "Public port", "Creates", "Joins"],
        [
            ["1", "kyc", "8080", "<code>kyc_lan</code>", "—"],
            ["2", "bank", "8081", "<code>bank_lan</code>", "<code>kyc_lan</code>"],
            ["3", "shop", "8082", "<code>shop_lan</code>", "<code>bank_lan</code>, <code>kyc_lan</code>"],
        ],
    ) + "<p class='note'>Inside the containers the apps listen on 5000, the bank's nginx on 80 and Keycloak on 8080. MinIO and Keycloak refuse <code>_</code> in a host name, so they are reached through the <code>shop-minio</code> and <code>kyc-proxy</code> network aliases.</p>"),
    ("Sign-In", "OpenID Connect authorization code flow, with PKCE in both apps.", steps([
        "The bank (<code>/account/login</code>) or the shop (<code>/api/auth/login</code>) sends the browser to Keycloak. Register adds <code>prompt=create</code>.",
        "Login asks for email and password. Register asks for Full Name, email, password and confirmation, and its button waits for the agreement checkbox.",
        "Keycloak returns a code to <code>/signin-oidc</code> or <code>/api/auth/callback</code>, and the app exchanges it through <code>kyc-proxy</code>.",
        "The app checks the issuer, audience, nonce and expiry, creates its local user on the first sign-in and takes the Full Name from Keycloak.",
        "The session is stored in Redis, with the tokens, and the browser only holds a cookie that points to it.",
    ]) + "<p class='note'>Browsers use <code>localhost:8080</code> and the apps use <code>kyc-proxy:5000</code>. Keycloak's dynamic backchannel keeps the issuer the same for both.</p>"),
    ("Sessions and Tokens", "What happens after sign-in.", table(
        ["Event", "Behaviour"],
        [
            ["Access token expires", "The next request renews the tokens with the refresh token, with no sign to the user."],
            ["Keycloak refuses the refresh", "The session is deleted and the user is sent to the login page. This covers logout elsewhere and blocked users."],
            ["Keycloak is unreachable", "The session stays and the next request tries again."],
            ["Logout", "The session ends and the browser goes to Keycloak's logout, which ends the shared sign-in too."],
            ["Second app in the same browser", "Keycloak's session signs the user in with no form."],
        ],
    ) + "<p class='note'>The bank does this in <code>KycTokenRefresher</code>, on cookie validation. The shop does it in <code>current_user</code>, on every request.</p>"),
    ("Edit Profile and Change Password", "Forms run at Keycloak, in a popup.", steps([
        "The menu item opens a popup through the login flow with <code>kc_action</code>: <code>UPDATE_PROFILE</code> or <code>ABS_UPDATE_PASSWORD</code>. Neither app has a settings page.",
        "Edit profile shows Full Name, Phone and Address, with the email read-only.",
        "Change password asks for the current password, the new one and its confirmation. The <code>abs</code> theme adds the field and a Keycloak provider checks it on the server.",
        "A saved form returns <code>kc_action_status=success</code>: the app session and the Keycloak session end, and the app behind goes to login.",
        "A cancelled form changes nothing: the popup closes and the app behind reloads.",
    ])),
    ("Inter-Bank Security Protocol", "Every call between a bank and CentralApi.", table(
        ["Step", "Detail"],
        [
            ["Sender signs", "With its own RSA private key"],
            ["Sender encrypts", "The payload with a new AES key and IV, and those with the receiver's RSA public key"],
            ["Message", "Base64 JSON: <code>EncryptedKey</code>, <code>EncryptedIv</code>, <code>Data</code>, <code>Signature</code>"],
            ["Receiver verifies", "The <code>[DecryptAndVerifyRequest]</code> filter decrypts, verifies and replaces the action's first argument with the JSON"],
            ["Failure", "403, and the action never runs"],
        ],
    ) + "<p class='note'>Keys use the .NET <code>&lt;RSAKeyValue&gt;</code> XML format; <code>docs/rsa-keygen.sh</code> generates them.</p>"),
    ("Routing", "What nginx and Kong send where.", table(
        ["Public path", "Internal path", "Target"],
        [
            ["<code>:8081/*</code>", "same", "<code>bank_web</code>"],
            ["<code>:8081/api/pay</code>", "<code>/pay</code>", "<code>bank_api</code>, with <code>X-Forwarded-Prefix: /api</code> from nginx"],
            ["<code>:8081/api/pay/continue</code>", "<code>/pay/continue</code>", "<code>bank_api</code>, with the same prefix"],
            ["<code>:8081/api/static/*</code>", "<code>/static/*</code>", "<code>bank_api</code>: fonts and Bootstrap of the pay pages"],
            ["<code>:8081/api/swagger/*</code>", "<code>/swagger/*</code>", "<code>bank_api</code>, Development only"],
            ["<code>:8081/api/*</code>", "<code>/api/*</code>", "<code>bank_api</code>"],
            ["Internal only: <code>bank_gateway:5000/bank/api/*</code>", "<code>/api/*</code>", "<code>bank_web</code>; nginx does not publish it"],
        ],
    ) + "<p class='note'><code>/pay</code> exists in the bank and in CentralApi, so CentralApi's page is public as <code>/api/pay</code>.</p>"),
    ("Payment Flows", "The two ways a shop gets paid.", table(
        ["Step", "Direct payment", "Card payment"],
        [
            ["1", "The shop signs a request and the browser posts it to <code>/api/pay</code>", "The cabinet sends the card to <code>shop_api</code>"],
            ["2", "CentralApi lists the banks and the customer picks one", "<code>shop_api</code> calls CentralApi through the gateway"],
            ["3", "The bank's <code>/pay</code> page confirms, signed in through Keycloak", "CentralApi finds the bank by the card's first 3 digits"],
            ["4", "The transfer goes bank → gateway → CentralApi → gateway → bank", "The bank checks the card and moves the money"],
            ["5", "The bank posts a signed result to the shop, which marks the order paid", "The shop marks the order paid, or Processing if the answer is not final"],
        ],
    ) + "<p class='note'>The receiving bank checks that the recipient's name equals the account owner's Full Name, so <code>RECIPIENT_NAME</code> in <code>shop.env</code> must match the shop's bank user.</p>"),
    ("Reliable Delivery", "How money moves once, even when a call gets no answer.", table(
        ["Case", "What happens"],
        [
            ["Internal transfer", "Both sides are one <code>SaveChanges</code>. <code>Balance</code> is a concurrency token, so parallel requests cannot overdraw an account"],
            ["Global transfer", "The money leaves the account together with a <code>PendingTransfers</code> row, in one transaction, so a crash cannot lose it"],
            ["Answer 2xx", "Delivered: the row ends"],
            ["Answer 4xx", "Refused: the money returns and the row ends, in one transaction"],
            ["5xx, 408, 429, timeout", "Unknown: the money stays held. Up to 3 tries in the request, then the <code>transfers.deliver</code> queue, which <code>transfers.retry</code> refills every 30 seconds, up to 2880 attempts, about a day"],
            ["Still no answer", "A critical log, and Retry and Refund buttons at <code>/administration/pendingtransfers</code>. Refund only after the other bank confirms the money did not arrive"],
            ["Lost message", "<code>PendingTransfers</code> is the record and RabbitMQ only speeds it up: <code>PendingTransferRetryService</code> picks up anything older than 5 minutes that nothing has tried for 2 minutes, also with no broker"],
            ["Repeated delivery", "The receiver credits a reference number once per account, with a filtered unique index behind the check"],
            ["Merchant with no final answer", "<code>shop_api</code> gives each attempt a random reference number and asks <code>POST /api/CardPayments/status</code>: when the bank's message arrives on <code>shop.payments</code>, on orders and payments, and every 5 minutes. The order is Processing and cannot be paid again"],
        ],
    ) + "<p class='note'>The state is Pending, Completed, Refunded or NotFound. CentralApi asks every bank, because the shop does not know the customer's bank for a direct payment. The shop's message is only a hint and it can only read its own queue, so a false message changes nothing.</p>"),
    ("Product Images", "Uploaded in the cabinet, served by the API.", table(
        ["Rule", "Detail"],
        [
            ["Storage", "A private MinIO bucket, <code>products</code>; only <code>shop_api</code> reaches it"],
            ["Upload", "Signed-in users, 5 MB at most, through <code>POST /api/products/images</code>"],
            ["Type", "PNG, JPEG or WebP, taken from the bytes and not from the file name"],
            ["Serving", "<code>GET /api/products/images/{key}</code>, cached for a year, with <code>nosniff</code>"],
            ["Editing and deleting", "In the cabinet's product list. A product with orders cannot be deleted, and a deleted product's image stays in the bucket"],
        ],
    )),
    ("Look and Assets", "One light look, served locally.", table(
        ["Area", "Detail"],
        [
            ["Palette", "The light colors of cbu.uz in the bank, the shop and the Keycloak theme: gold #ffc700 and #e4a216, deep gold #8a5a00 and cream #f6f5e7. There is no dark mode."],
            ["Fonts", "Inter, in <code>wwwroot/fonts</code>, <code>shop/app/public/fonts</code> and CentralApi's <code>wwwroot/static</code>"],
            ["Icons", "Font Awesome in the bank, Ant Design icons in the cabinet and inline SVG in the shop site"],
            ["Styles", "Bootstrap in the bank and CentralApi, Tailwind in <code>shop/app/public/js/tailwind.js</code>"],
            ["CDNs", "None: browsers load nothing from outside the stacks. Building the images still needs the registries."],
            ["Bank menu", "Overview, Transfers, Cards, Administration and Settings, with the Full Name and a red Log out below"],
            ["Shop", "A landing page and a <code>/products</code> catalog with no detail page; the cabinet has Overview, Shopping and Settings"],
        ],
    )),
    ("Data Storage", "Where each piece of data lives.", table(
        ["Store", "Holds", "Volume"],
        [
            ["SQL Server", "Users, accounts, cards, transfers, pending transfers, known banks", "<code>bank_mssql_data</code>"],
            ["Redis (bank)", "Login tickets with tokens, cache, Data Protection keys", "<code>bank_redis_data</code>"],
            ["RabbitMQ", "Delivery, retry and settled payment messages", "<code>bank_rabbit_data</code>"],
            ["PostgreSQL", "Shop users, products, orders, open payment attempts", "<code>shop_pg_data</code>"],
            ["Redis (shop)", "Sessions, tokens, login state, product cache", "<code>shop_redis_data</code>"],
            ["MinIO", "Product images", "<code>shop_minio_data</code>"],
            ["H2", "Keycloak users, credentials, sessions, realm", "<code>kyc_kyc_data</code>"],
        ],
    ) + "<p class='note'>The bank and CentralApi migrate with EF Core on startup. The shop creates its tables in <code>seed.py</code>. The <code>abs</code> realm is imported on first start only.</p>"),
    ("Security", "Controls, and what is deliberately left for a real deployment.", table(
        ["Area", "Control"],
        [
            ["Passwords", "Only in Keycloak; the bank and the shop store none"],
            ["Change password", "The <code>ABS_UPDATE_PASSWORD</code> required action checks the current password on the server, not only in the form"],
            ["OIDC", "State, nonce and PKCE; the client secret stays on the server"],
            ["Redirects", "<code>next</code> and <code>returnUrl</code> stay on the site; Keycloak allows only registered URIs"],
            ["Sessions", "HttpOnly cookies that point to Redis, with no tokens in the browser"],
            ["Rate limit", "60 calls a minute per IP on public <code>/api/</code> in nginx, answered with 429"],
            ["Uploads", "Type from the bytes, private bucket, <code>nosniff</code>"],
            ["Messages", "RabbitMQ publishes no port, the <code>shop</code> user can only read its own queue, and the shop treats a message as a hint"],
        ],
    )),
    ("Testing", "What runs, and on what.", table(
        ["Suite", "Technology", "Runs on"],
        [
            ["Bank and CentralApi (4 projects)", "xUnit, FluentAssertions, Moq", "EF Core InMemory, <code>WebApplicationFactory</code>"],
            ["Shop API", "pytest", "SQLite, fake Redis, fake storage"],
            ["Shop UI", "Playwright, TypeScript", "The whole stack, real Keycloak, CentralApi stub"],
            ["Keycloak", "JUnit, Java", "Real Keycloak on H2: login, register, tokens, sessions"],
        ],
    ) + "<p class='note'>SQL Server and RabbitMQ are not covered by the test suites: they are checked by running the real stack.</p>"),
    ("Operations", "Running it.", "<pre>docker compose -f kyc/docker-compose.yml up -d --build\ndocker compose -f bank/docker-compose.yml up -d --build\ndocker compose -f shop/docker-compose.yml up -d --build\n\ncd bank && dotnet test\ndocker compose -f shop/docker-compose.test.yml run --rm --build shop_api_tests\ndocker compose -f shop/docker-compose.test.yml up --build --abort-on-container-failure --exit-code-from shop_ui_tests shop_ui_tests\ndocker compose -f kyc/docker-compose.test.yml up --build --abort-on-container-failure --exit-code-from kyc_tests kyc_tests</pre>" + table(
        ["Account", "Password", "Use"],
        [["<code>demo@abs.uz</code>", "123456", "Default user: bank administrator with 10,000,000 UZS"], ["<code>admin</code>", "123456", "Keycloak root user"]],
    )),
    ("Risks and Roadmap", "What a real deployment still needs.", table(
        ["Risk", "Now", "Next"],
        [
            ["Secrets in a public repository", "Env files with development values are committed", "Rotate every value and move them to a secret store"],
            ["Email is not verified", "Users are matched by email, so anyone can claim an unused address", "Turn on Keycloak's <code>verifyEmail</code>, with SMTP"],
            ["H2 for Keycloak", "Single instance, development use only", "Move to a production database"],
            ["Plain HTTP", "Everything runs on localhost", "Terminate TLS at nginx and require it in Keycloak"],
            ["SQL Server on Apple Silicon", "Runs under amd64 emulation", "Run it natively on the servers"],
            ["Recipient name check", "Full Name comes from Keycloak on each sign-in, so renaming the shop's bank user stops its payments", "Match accounts by id, not by name"],
            ["Mixed bank versions", "A bank without the once-only check could credit a repeated delivery twice", "Update every connected bank before enabling retries"],
            ["Orphaned images", "A deleted product leaves its image", "Delete the object with the product"],
        ],
    )),
]


CSS = """
@page { size: A4 landscape; margin: 0; }
:root { --ink:#212529; --muted:#6c757d; --line:#e9e4d0; --gold:#ffc700; --deep:#8a5a00; --cream:#f6f5e7; }
* { box-sizing:border-box; margin:0; }
body { -webkit-print-color-adjust:exact; print-color-adjust:exact; font-family:Inter,system-ui,sans-serif; color:var(--ink); font-size:10.5px; line-height:1.45; }
.page { width:297mm; height:210mm; position:relative; padding:17mm 19mm 16mm; page-break-after:always; overflow:hidden; border-top:2mm solid var(--gold); }
.page:last-child { page-break-after:auto; }
.num { display:inline-flex; width:8mm; height:8mm; border-radius:2mm; background:var(--gold); font-weight:700; align-items:center; justify-content:center; font-size:12px; margin-right:3mm; vertical-align:middle; }
h2 { display:inline; font-size:18px; font-weight:700; vertical-align:middle; }
.sub { color:#495057; margin:1.5mm 0 6mm 11mm; font-size:11px; }
table { width:100%; border-collapse:collapse; }
th { background:#fbfaf3; text-align:left; padding:2.2mm 2.8mm; border-bottom:1px solid #d8d2bb; font-weight:600; }
td { padding:2.2mm 2.8mm; border-bottom:1px solid var(--line); vertical-align:top; }
td:first-child { font-weight:600; }
code, pre { font-family:"JetBrains Mono",monospace; font-size:.92em; color:var(--deep); }
pre { background:#fbfaf3; border:1px solid var(--line); border-radius:6px; padding:4mm; margin-bottom:5mm; line-height:1.6; white-space:pre-wrap; }
.note { margin-top:4mm; color:#495057; }
.steps { padding-left:6mm; }
.steps li { margin-bottom:3mm; padding-left:1.5mm; font-size:11px; }
.steps li::marker { color:var(--deep); font-weight:700; }
.foot { position:absolute; left:19mm; right:19mm; bottom:8mm; border-top:1px solid var(--line); padding-top:1.5mm; display:flex; justify-content:space-between; color:var(--muted); font-size:8.5px; }
.cover { display:grid; grid-template-columns:1fr 125mm; gap:14mm; margin-top:20mm; }
.title { border-left:1.2mm solid var(--gold); padding-left:7mm; }
.title h1 { font-size:30px; font-weight:800; letter-spacing:-.7px; }
.title h3 { font-weight:500; color:#495057; font-size:14px; margin:2mm 0 3mm; }
.title small { color:var(--muted); }
.title p { margin-top:9mm; font-size:11.5px; }
.stats { display:grid; grid-template-columns:1fr 1fr; gap:3mm; }
.stats div { background:#fbfaf3; border:1px solid var(--line); border-radius:6px; padding:3mm 4mm; }
.stats b { font-size:17px; color:var(--deep); display:block; }
.stats span { color:#495057; font-size:9px; }
.toc { position:absolute; left:19mm; right:19mm; top:125mm; }
.toc h4 { margin-bottom:3mm; font-size:12px; }
.toc ol { columns:4; padding-left:5mm; color:#343a40; }
.toc li { margin-bottom:1.6mm; }
.diagram { width:100%; border:1px solid var(--line); border-radius:6px; }
"""


def page(number, content):
    return f"<section class='page'>{content}<div class='foot'><span>BankSystem · Infrastructure and Architecture</span><span>{number}</span></div></section>"


def cover():
    toc = "".join(f"<li>{escape(title)}</li>" for title, _, _ in SECTIONS)
    return page(1, f"""
<div class='cover'>
  <div class='title'>
    <h1>BankSystem</h1>
    <h3>Infrastructure and Architecture</h3>
    <small>An overview for software architects · November 2026</small>
    <p>A web bank that connects to other banks through a central hub, a sample shop that takes card and direct payments through it, and one central sign-in for both. Each part is an independent Docker Compose stack.</p>
  </div>
  <div class='stats'>
    <div><b>3 stacks</b><span>kyc, bank, shop, deployed on their own</span></div>
    <div><b>3 ports</b><span>8080 Keycloak, 8081 bank, 8082 shop</span></div>
    <div><b>RSA + AES</b><span>on every bank to hub call</span></div>
    <div><b>1 sign-in</b><span>OpenID Connect through Keycloak</span></div>
    <div><b>60 / min</b><span>public API calls per client IP</span></div>
    <div><b>.NET 10</b><span>with Python, React, TypeScript</span></div>
  </div>
</div>
<div class='toc'><h4>Contents</h4><ol>{toc}</ol></div>""")


def main():
    pages = [cover(), page(2, "<span class='num'>A</span><h2>Architecture</h2><p class='sub'>The whole system on one page.</p><img class='diagram' src='../infra.png'>")]
    for index, (title, subtitle, body) in enumerate(SECTIONS, start=1):
        pages.append(page(index + 2, f"<span class='num'>{index}</span><h2>{escape(title)}</h2><p class='sub'>{escape(subtitle)}</p>{body}"))

    fonts = "https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600;700;800&family=JetBrains+Mono:wght@400;500&display=swap"
    html = f"<!doctype html><html lang='en'><head><meta charset='utf-8'><title>BankSystem Infrastructure</title><link href='{fonts}' rel='stylesheet'><style>{CSS}</style></head><body>{''.join(pages)}</body></html>"
    Path(__file__).with_name("tech.html").write_text(html, encoding="utf-8")


if __name__ == "__main__":
    main()
