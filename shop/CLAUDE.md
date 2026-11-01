# Shop

Guidance for DemoShop; the root `CLAUDE.md` has the overview and commands.

## MinIO (shop)
Product images uploaded in the cabinet go to the `products` bucket through `shop/api/src/storage.py`. The bucket is private: `shop_api` serves images at `/api/products/images/{key}`. The image is `cgr.dev/chainguard/minio`, because the official one is not on the registries. A product with orders cannot be deleted.

## Payments without a final answer
The bank keeps delivering a payment after it has answered, so `shop/api/src/reconcile.py` follows it. Each attempt is a `PaymentAttempt` row with its own random reference number, saved before the money moves and sent in the payment. A direct payment reuses an open attempt, so a second tab gets the same number and the bank refuses it. A card answer of 202, a 5xx or a network error leaves the order `processing`, which cannot be paid again. Open attempts are checked against `POST /api/CardPayments/status` when the bank's message arrives on `shop.payments` (`src/messages.py`, off without `RABBIT_URL`), when the orders are listed, before a new payment and in the background every `RECONCILE_INTERVAL_SECONDS` (300 in `shop.env`). A message is only a hint, so a false one changes nothing: `Completed` pays the order, `Refunded` ends the attempt and `Pending` holds the order. `NotFound` ends a card attempt after 5 minutes and a direct one after an hour, since the customer may still be on the bank's page. With no answer nothing changes.
