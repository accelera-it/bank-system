import json

import pytest

CARD = {"number": "1012223334445556", "name": "Buyer", "expiry_date": "12/30", "security_code": "123"}


async def order_id(client, product) -> str:
    response = await client.post("/api/orders", json={"product_id": product.id})
    assert response.status_code == 201
    return response.json()["id"]


async def test_orders_need_a_session(client):
    assert (await client.get("/api/orders")).status_code == 401


async def test_new_order_is_pending_and_listed(signed_in, product):
    created = await order_id(signed_in, product)

    orders = (await signed_in.get("/api/orders")).json()

    assert [o["id"] for o in orders] == [created]
    assert orders[0]["payment_status"] == "pending"
    assert orders[0]["product"]["name"] == "Laptop"


async def test_order_of_unknown_product_is_refused(signed_in):
    response = await signed_in.post("/api/orders", json={"product_id": "missing"})

    assert response.status_code == 404


async def test_order_of_another_user_is_hidden(signed_in, product):
    created = await order_id(signed_in, product)
    await signed_in.post("/api/auth/register", json={"name": "Other", "email": "other@abs.uz", "password": "123456"})

    assert (await signed_in.get(f"/api/orders/{created}")).status_code == 404
    assert (await signed_in.post(f"/api/orders/{created}/card-payment", json=CARD)).status_code == 404


async def test_card_payment_sends_the_contract_of_central_api(signed_in, product, central_api):
    created = await order_id(signed_in, product)

    response = await signed_in.post(f"/api/orders/{created}/card-payment", json=CARD)

    assert response.status_code == 200
    assert response.json()["payment_status"] == "completed"
    sent = json.loads(central_api["requests"][0].content)
    assert sent["Number"] == CARD["number"]
    assert sent["ExpiryDate"] == "12/30"
    assert sent["SecurityCode"] == "123"
    assert sent["Amount"] == 8900000
    assert sent["DestinationBankAccountUniqueId"] == "ABCJ98131785"


async def test_refused_card_leaves_the_order_pending(signed_in, product, central_api):
    central_api["status"] = 400
    created = await order_id(signed_in, product)

    response = await signed_in.post(f"/api/orders/{created}/card-payment", json=CARD)

    assert response.status_code == 402
    assert (await signed_in.get(f"/api/orders/{created}")).json()["payment_status"] == "pending"


async def test_paid_order_cannot_be_paid_again(signed_in, product, central_api):
    created = await order_id(signed_in, product)
    await signed_in.post(f"/api/orders/{created}/card-payment", json=CARD)

    response = await signed_in.post(f"/api/orders/{created}/card-payment", json=CARD)

    assert response.status_code == 409
    assert len(central_api["requests"]) == 1


@pytest.mark.parametrize(
    "field,value",
    [("number", "123"), ("expiry_date", "13/30"), ("security_code", "12")],
)
async def test_invalid_card_is_refused_before_central_api(signed_in, product, central_api, field, value):
    created = await order_id(signed_in, product)

    response = await signed_in.post(f"/api/orders/{created}/card-payment", json={**CARD, field: value})

    assert response.status_code == 422
    assert central_api["requests"] == []
