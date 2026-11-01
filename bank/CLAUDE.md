# Bank

Guidance for the bank and CentralApi; the root `CLAUDE.md` has the overview and commands.

## Redis
`RedisServiceCollectionExtensions.AddRedisSessions` in BankSystem.Web puts `IDistributedCache`, login tickets (`RedisTicketStore`) and Data Protection keys in Redis, so the auth cookie only carries a key. Without `ConnectionStrings:Redis` (tests, plain `dotnet run`) the cache lives in memory. CentralApi has no logins or cache, so it does not use Redis.

## Convention-based DI
`bank/app/src/BankSystem.Web/Infrastructure/Extensions/ServiceCollectionExtensions.cs` registers every class `Foo` that implements an interface named `IFoo` — scoped for the Services and Common assemblies, transient for the web assembly. New services need no manual registration as long as they follow the `Foo`/`IFoo` naming.

## Convention-based Mapster mapping
`bank/app/core/BankSystem.Common/Mapping/MappingConfig.cs` scans the assemblies passed to `AddMapping`/`MappingConfig.Create`:
- types implementing `IMapWith<T>` get a bidirectional map with `T`;
- types implementing `IHaveCustomMapping` configure their own maps.
Add mappings by implementing these interfaces on the DTO/view model.

## RabbitMQ
`bank_rabbit` (`bank/data/rabbitmq`) holds the queues, declared with the users `bank` and `shop` in `definitions.json`, whose password hashes match `ConnectionStrings__RabbitMq` in `bank.env` and `RABBIT_URL` in `shop.env`. `PendingTransfers` is the record and the broker only speeds it up: a failed publish is logged and never fails a payment, and `PendingTransferRetryService` delivers rows older than 5 minutes that nothing has tried for 2 minutes (`LastAttemptOn`), so it only acts when the queue is not delivering. The node keeps its data under a host name, which is why `bank_rabbit` has a fixed `hostname`. `definitions.json` is applied on every start: new queues, users and policies arrive with a restart, but an existing queue keeps its arguments, so TTLs and dead-lettering live in policies. Without `ConnectionStrings:RabbitMq` (tests, plain `dotnet run`) there is no broker and that service does all the retrying. When a transfer settles, the bank publishes its reference number to `payments.settled`, which feeds `shop.payments`, the only queue the `shop` user can read.

## Inter-bank security protocol
All bank ↔ CentralApi traffic uses hybrid encryption + RSA signatures (`bank/app/core/BankSystem.Common/Utils/SignatureVerificationUtil.cs`, `CryptographyExtensions.cs`, `RsaExtensions.cs`):
- Sender signs with its private key and encrypts an AES key/IV with the recipient's public key; the payload is a Base64 JSON `{ EncryptedKey, EncryptedIv, Data, Signature }`.
- Receivers use a `[DecryptAndVerifyRequest]` action filter (one in BankSystem.Web, one in CentralApi) that decrypts, verifies, and **replaces the action's first argument** with the decrypted JSON string, returning 403 on failure. API actions therefore take `[FromBody] string data` and deserialize it themselves.
- CentralApi serves Swagger at `/swagger` (`/api/swagger/index.html` through nginx) in Development only; it lists endpoints, and unsigned calls still get 403.
- Bank API endpoints: `bank/app/src/BankSystem.Web/Api/` (`ReceiveMoneyTransfers`, `CardPayments`). CentralApi endpoints: `ReceiveTransactions`, `CardPayments`, `Payments`.

## Money transfers
- An internal transfer is one `SaveChanges`, so both sides are saved or neither (`MoneyTransferService.TransferAsync`). `BankAccount.Balance` is a concurrency token: a save that lost a race reloads the accounts and tries again, up to 3 times, and each attempt builds new entities because a failed save leaves the old account on the navigation.
- A global transfer takes the money first and saves a `PendingTransfers` row in the same transaction, so a crash cannot lose it (`GlobalTransferHelper`). CentralApi's 2xx ends the row; 4xx is a refusal and refunds it, in one transaction with removing the row; 5xx, 408, 429, timeouts and connection errors are unknown outcomes, which never refund. They are delivered again up to 3 times within the request, then `PendingTransferConsumer` takes them from the `transfers.deliver` queue, and `transfers.retry` hands each one back after 30 seconds, up to 2880 attempts, about a day, after which it logs critical. Administrators see these rows at `/administration/pendingtransfers`, where they can retry or refund; a manual refund is only right once the other bank has confirmed the money did not arrive.
- CentralApi answers 502, not 400, when the destination bank fails with a 5xx, so the sender does not take a failure for a refusal.
- Delivering again is safe because a bank credits a reference number once per account: `ReceiveMoneyTransfersController` answers OK for a repeat before it checks the recipient's name, and a filtered unique index (`Amount > 0`) settles a race. Every connected bank needs this, or a retry can credit twice.
- A merchant can follow a payment it has no final answer for. It sets its own random `ReferenceNumber` on the payment, a card payment with an unknown outcome answers 202, and `POST /api/CardPayments/status` on CentralApi (public, unsigned, takes the reference number and returns only a state) asks each bank through the signed `POST api/CardPayments/status`. The state is `Pending`, `Completed`, `Refunded` or `NotFound`, and CentralApi answers 502 rather than `NotFound` if a bank did not reply. A used reference number is refused, because merchants choose it.
- EF Core InMemory has no transactions, so a refused save keeps its rows there: tests cannot assert rollback, only balances.

## Configuration and multi-bank setup
- Bank identity comes from the `BankConfiguration` section (name, `UniqueIdentifier` = swift code, country, `First3CardDigits`, own RSA `Key`, `CentralApiPublicKey`, `CentralApiAddress`). It is validated in `PostConfigure`, so startup throws if a value is missing.
- CentralApi knows banks through its `Banks` table (`bank/app/src/CentralApi.Models/Bank.cs`: public `ApiKey`, `ApiAddress`, `PaymentUrl`, `CardPaymentUrl`, `BankIdentificationCardNumbers`). It routes transfers by matching name + swift code + country, and card payments by the card's first 3 digits. A default bank is seeded in `bank/app/src/CentralApi/Infrastructure/ApplicationBuilderExtensions.cs`, at the address in the `DefaultBankBaseUrl` setting, with its browser-facing payment page at `DefaultBankPublicUrl`.
