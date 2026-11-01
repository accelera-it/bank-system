"""Hears from the bank that a payment has settled, so the order need not wait for the next pass.

A message is only a hint: the outcome still comes from CentralApi, so a false one changes nothing.
If messages are lost, reconcile_forever finds the outcome anyway.
"""

import asyncio
import json
import logging

import aio_pika
import httpx
from sqlalchemy.ext.asyncio import AsyncSession, async_sessionmaker

from src.config import Settings
from src.models import Order, PaymentAttempt
from src.reconcile import reconcile_order

logger = logging.getLogger(__name__)

QUEUE = "shop.payments"
RECONNECT_SECONDS = 10


async def settle(
    body: bytes, sessions: async_sessionmaker[AsyncSession], client: httpx.AsyncClient, settings: Settings
) -> None:
    try:
        reference = json.loads(body)["ReferenceNumber"]
    except (ValueError, KeyError, TypeError):
        return

    async with sessions() as db:
        attempt = await db.get(PaymentAttempt, reference)
        order = await db.get(Order, attempt.order_id) if attempt is not None else None
        if order is not None:
            await reconcile_order(order, db, client, settings)


async def consume_settled(sessions: async_sessionmaker[AsyncSession], settings: Settings) -> None:
    async with httpx.AsyncClient(timeout=30) as client:
        while True:
            try:
                connection = await aio_pika.connect_robust(settings.rabbit_url)
                async with connection:
                    channel = await connection.channel()
                    queue = await channel.declare_queue(QUEUE, passive=True)
                    async with queue.iterator() as messages:
                        async for message in messages:
                            async with message.process():
                                await settle(message.body, sessions, client, settings)
            except asyncio.CancelledError:
                raise
            except Exception:
                logger.exception("The payment queue is not available")
            await asyncio.sleep(RECONNECT_SECONDS)
