import base64
import json

from src.payments import generate_payment_request, load_private_key, load_public_key, process_payment_result, sign, verify


def build_result(info_json, site, central, bank, success=True, proof_signer=None):
    """What the bank sends back through the browser after a direct payment."""
    proof = json.dumps({"BankPublicKey": bank.public, "PaymentInfoSignature": sign(info_json, site.key)})
    confirmation = json.dumps(
        {"PaymentProofSignature": sign(proof, (proof_signer or central).key), "Success": success}
    )
    result = {
        "PaymentInfo": info_json,
        "PaymentProof": proof,
        "PaymentConfirmation": confirmation,
        "PaymentConfirmationSignature": sign(confirmation, bank.key),
    }
    return base64.b64encode(json.dumps(result).encode()).decode()


async def direct_request(client, product):
    order = (await client.post("/api/orders", json={"product_id": product.id})).json()
    response = await client.post(f"/api/orders/{order['id']}/direct-payment")
    assert response.status_code == 200
    return order["id"], response.json()


def test_keys_load_from_the_dotnet_xml_format(site_keys):
    signature = sign("data", load_private_key(site_keys.private))

    assert verify("data", signature, load_public_key(site_keys.public))
    assert not verify("other", signature, load_public_key(site_keys.public))


async def test_request_is_signed_for_central_api(signed_in, product, site_keys):
    order_id, payment = await direct_request(signed_in, product)

    request = json.loads(base64.b64decode(payment["data"]))

    assert payment["url"] == "http://central.test/pay"
    assert payment["field"] == "data"
    assert request["ReturnUrl"] == "http://shop.test/api/payments/direct/confirmation"
    assert request["PublicKey"] == site_keys.public
    assert verify(request["PaymentInfo"], request["PaymentInfoSignature"], load_public_key(site_keys.public))
    info = json.loads(request["PaymentInfo"])
    assert info["OrderId"] == order_id
    assert info["Amount"] == 8900000
    assert info["DestinationBankSwiftCode"] == "ABC"


async def test_valid_confirmation_completes_the_order(signed_in, product, site_keys, central_keys, bank_keys):
    order_id, payment = await direct_request(signed_in, product)
    info_json = json.loads(base64.b64decode(payment["data"]))["PaymentInfo"]

    response = await signed_in.post(
        "/api/payments/direct/confirmation",
        data={"data": build_result(info_json, site_keys, central_keys, bank_keys)},
    )

    assert response.status_code == 303
    assert response.headers["location"] == "http://shop.test/cabinet/orders"
    assert (await signed_in.get(f"/api/orders/{order_id}")).json()["payment_status"] == "completed"


async def test_confirmation_without_data_is_refused(client):
    assert (await client.post("/api/payments/direct/confirmation", data={})).status_code == 400


async def test_tampered_payment_info_is_refused(site_keys, central_keys, bank_keys):
    info = json.dumps({"OrderId": "1"})
    result = json.loads(base64.b64decode(build_result(info, site_keys, central_keys, bank_keys)))
    result["PaymentInfo"] = json.dumps({"OrderId": "2"})
    tampered = base64.b64encode(json.dumps(result).encode()).decode()

    assert process_payment_result(tampered, site_keys.private, central_keys.public) is None


async def test_bank_key_not_vouched_for_by_central_api_is_refused(site_keys, central_keys, bank_keys):
    data = build_result(json.dumps({"OrderId": "1"}), site_keys, central_keys, bank_keys, proof_signer=bank_keys)

    assert process_payment_result(data, site_keys.private, central_keys.public) is None


async def test_unsuccessful_payment_is_refused(site_keys, central_keys, bank_keys):
    data = build_result(json.dumps({"OrderId": "1"}), site_keys, central_keys, bank_keys, success=False)

    assert process_payment_result(data, site_keys.private, central_keys.public) is None


async def test_garbage_is_refused(site_keys, central_keys):
    assert process_payment_result("not base64!", site_keys.private, central_keys.public) is None


def test_generated_request_decodes(site_keys):
    data = generate_payment_request({"OrderId": "1"}, site_keys.private, "http://shop.test/back")

    assert json.loads(base64.b64decode(data))["ReturnUrl"] == "http://shop.test/back"
