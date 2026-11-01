"""A message from the bank is only a hint: the outcome comes from CentralApi."""

import json

import httpx
import pytest

from src.messages import settle
from src.models import Order, PaymentAttempt, PaymentMethod, PaymentStatus


def central(state: str) -> httpx.AsyncClient:
    def handler(_: httpx.Request) -> httpx.Response:
        return httpx.Response(200, json={"state": state})

    return httpx.AsyncClient(transport=httpx.MockTransport(handler))


@pytest.fixture
async def open_order(db_factory, product) -> str:
    async with db_factory() as db:
        order = Order(id="order", product_id=product.id, user_id="user", payment_status=PaymentStatus.processing)
        db.add(order)
        db.add(PaymentAttempt(reference="ref", order_id="order", method=PaymentMethod.card))
        await db.commit()
    return "order"


async def status_of(db_factory, order_id: str) -> PaymentStatus:
    async with db_factory() as db:
        return (await db.get(Order, order_id)).payment_status


async def test_message_settles_the_order(open_order, db_factory, settings):
    async with central("Completed") as client:
        await settle(json.dumps({"ReferenceNumber": "ref"}).encode(), db_factory, client, settings)

    assert await status_of(db_factory, open_order) == PaymentStatus.completed


async def test_false_message_changes_nothing(open_order, db_factory, settings):
    async with central("Pending") as client:
        await settle(json.dumps({"ReferenceNumber": "ref"}).encode(), db_factory, client, settings)

    assert await status_of(db_factory, open_order) == PaymentStatus.processing


@pytest.mark.parametrize("body", [b"not json", b"[]", b"{}", json.dumps({"ReferenceNumber": "other"}).encode()])
async def test_bad_or_foreign_message_is_ignored(open_order, db_factory, settings, body):
    async with central("Completed") as client:
        await settle(body, db_factory, client, settings)

    assert await status_of(db_factory, open_order) == PaymentStatus.processing
