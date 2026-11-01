"""A payment without a clear answer: the bank keeps trying, and the shop asks how it ended."""

import json
from datetime import timedelta

import httpx
import pytest
from sqlalchemy import select, update

from src.models import Order, PaymentAttempt, PaymentStatus, utc_now
from src.reconcile import reconcile_open_orders
from test_orders import CARD, order_id, status_of


async def pay(client, created: str) -> httpx.Response:
    return await client.post(f"/api/orders/{created}/card-payment", json=CARD)


@pytest.mark.parametrize("answer", [202, 500, 502, httpx.ReadTimeout("slow")])
async def test_unclear_answer_holds_the_order(signed_in, product, central_api, answer):
    central_api["status"] = answer
    central_api["state"] = "Pending"
    created = await order_id(signed_in, product)

    response = await pay(signed_in, created)

    assert response.status_code == 200
    assert response.json()["payment_status"] == "processing"
    assert await status_of(signed_in, created) == "processing"


async def test_processing_order_cannot_be_paid_again(signed_in, product, central_api):
    central_api["status"] = 202
    central_api["state"] = "Pending"
    created = await order_id(signed_in, product)
    await pay(signed_in, created)
    central_api["status"] = 200

    response = await pay(signed_in, created)

    assert response.status_code == 409
    assert len(central_api["requests"]) == 1
    assert (await signed_in.post(f"/api/orders/{created}/direct-payment")).status_code == 409


async def test_bank_delivering_later_pays_the_order(signed_in, product, central_api, db_factory):
    central_api["status"] = 202
    central_api["state"] = "Pending"
    created = await order_id(signed_in, product)
    await pay(signed_in, created)

    central_api["state"] = "Completed"

    assert await status_of(signed_in, created) == "completed"
    async with db_factory() as db:
        assert list(await db.scalars(select(PaymentAttempt))) == []


async def test_refund_lets_the_customer_pay_again(signed_in, product, central_api):
    central_api["status"] = 202
    central_api["state"] = "Pending"
    created = await order_id(signed_in, product)
    await pay(signed_in, created)

    central_api["state"] = "Refunded"
    assert await status_of(signed_in, created) == "pending"

    central_api["status"] = 200
    assert (await pay(signed_in, created)).status_code == 200


async def test_silent_central_api_changes_nothing(signed_in, product, central_api):
    central_api["status"] = 202
    central_api["state"] = "Pending"
    created = await order_id(signed_in, product)
    await pay(signed_in, created)

    central_api["state_status"] = 502

    assert await status_of(signed_in, created) == "processing"


async def test_unknown_card_payment_is_given_up_after_the_grace(signed_in, product, central_api, db_factory):
    central_api["status"] = 502
    created = await order_id(signed_in, product)
    await pay(signed_in, created)
    assert await status_of(signed_in, created) == "processing"

    async with db_factory() as db:
        await db.execute(update(PaymentAttempt).values(created_at=utc_now() - timedelta(minutes=6)))
        await db.commit()

    assert await status_of(signed_in, created) == "pending"


async def test_abandoned_direct_payment_keeps_the_order_payable(signed_in, product, central_api):
    created = await order_id(signed_in, product)

    assert (await signed_in.post(f"/api/orders/{created}/direct-payment")).status_code == 200

    assert await status_of(signed_in, created) == "pending"
    assert len(central_api["asked"]) == 1


async def test_direct_payment_refused_while_the_bank_holds_it(signed_in, product, central_api):
    created = await order_id(signed_in, product)
    await signed_in.post(f"/api/orders/{created}/direct-payment")

    central_api["state"] = "Pending"

    assert await status_of(signed_in, created) == "processing"


async def test_second_tab_reuses_the_reference_so_the_bank_refuses_it(signed_in, product, db_factory):
    created = await order_id(signed_in, product)

    first = await signed_in.post(f"/api/orders/{created}/direct-payment")
    second = await signed_in.post(f"/api/orders/{created}/direct-payment")

    assert first.status_code == second.status_code == 200
    async with db_factory() as db:
        assert len(list(await db.scalars(select(PaymentAttempt)))) == 1


async def test_each_attempt_has_its_own_reference(signed_in, product, central_api):
    central_api["status"] = 400
    created = await order_id(signed_in, product)
    await pay(signed_in, created)
    await pay(signed_in, created)

    first, second = [json.loads(request.content)["ReferenceNumber"] for request in central_api["requests"]]

    assert first != second


async def test_background_pass_settles_orders_without_a_visitor(
    signed_in, product, central_api, db_factory, settings
):
    central_api["status"] = 202
    central_api["state"] = "Pending"
    created = await order_id(signed_in, product)
    await pay(signed_in, created)
    central_api["state"] = "Completed"

    def handler(request: httpx.Request) -> httpx.Response:
        return httpx.Response(200, json={"state": "Completed"})

    async with httpx.AsyncClient(transport=httpx.MockTransport(handler)) as client:
        await reconcile_open_orders(db_factory, client, settings)

    async with db_factory() as db:
        assert (await db.get(Order, created)).payment_status == PaymentStatus.completed
