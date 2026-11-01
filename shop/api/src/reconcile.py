"""Finds out how a payment ended when the shop did not get a clear answer.

The bank keeps trying a transfer after it has answered, so the order may be paid later. Each
attempt carries a random reference number, and CentralApi tells where that transfer stands.
"""

import asyncio
import logging
from datetime import UTC, datetime, timedelta

import httpx
from sqlalchemy import delete, select
from sqlalchemy.ext.asyncio import AsyncSession, async_sessionmaker

from src.config import Settings
from src.models import Order, PaymentAttempt, PaymentMethod, PaymentStatus, utc_now

logger = logging.getLogger(__name__)

# How long the bank may not know a reference number before the shop gives up on it. A card
# payment reaches the bank at once, while a direct one waits for the customer on the bank's page.
NOT_FOUND_GRACE = {PaymentMethod.card: timedelta(minutes=5), PaymentMethod.direct: timedelta(hours=1)}


async def _state(reference: str, client: httpx.AsyncClient, settings: Settings) -> str | None:
    """Returns the bank's answer, or None when there is none."""
    try:
        response = await client.post(
            f"{settings.central_api_card_payments_url}/status", json={"ReferenceNumber": reference}
        )
        return response.json().get("state") if response.status_code == 200 else None
    except (httpx.HTTPError, ValueError, AttributeError):
        return None


def _age(attempt: PaymentAttempt) -> timedelta:
    created = attempt.created_at
    return utc_now() - (created if created.tzinfo else created.replace(tzinfo=UTC))


async def complete_order(order: Order, db: AsyncSession) -> None:
    order.payment_status = PaymentStatus.completed
    await db.execute(delete(PaymentAttempt).where(PaymentAttempt.order_id == order.id))
    await db.commit()


async def reconcile_order(order: Order, db: AsyncSession, client: httpx.AsyncClient, settings: Settings) -> None:
    if order.payment_status == PaymentStatus.completed:
        return

    attempts = await db.scalars(select(PaymentAttempt).where(PaymentAttempt.order_id == order.id))
    unsettled = False
    for attempt in list(attempts):
        state = await _state(attempt.reference, client, settings)
        if state == "Completed":
            await complete_order(order, db)
            return

        gone = state == "NotFound" and _age(attempt) > NOT_FOUND_GRACE[attempt.method]
        if state == "Refunded" or gone:
            await db.delete(attempt)
        elif (
            state == "Pending"
            or attempt.method == PaymentMethod.card
            or order.payment_status == PaymentStatus.processing
        ):
            unsettled = True

    order.payment_status = PaymentStatus.processing if unsettled else PaymentStatus.pending
    await db.commit()


async def reconcile_open_orders(
    sessions: async_sessionmaker[AsyncSession], client: httpx.AsyncClient, settings: Settings
) -> None:
    async with sessions() as db:
        order_ids = await db.scalars(select(PaymentAttempt.order_id).distinct())
        for order_id in list(order_ids):
            order = await db.get(Order, order_id)
            if order is not None:
                await reconcile_order(order, db, client, settings)


async def reconcile_forever(sessions: async_sessionmaker[AsyncSession], settings: Settings) -> None:
    async with httpx.AsyncClient(timeout=30) as client:
        while True:
            await asyncio.sleep(settings.reconcile_interval_seconds)
            try:
                await reconcile_open_orders(sessions, client, settings)
            except Exception:
                logger.exception("Reconciling open orders failed")
