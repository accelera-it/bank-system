"""Merchant side of the CentralApi payment protocol.

The JSON documents are signed as strings and travel as strings, so what gets verified is
exactly what was signed. Keys use the .NET <RSAKeyValue> XML format of the other services.
"""

import base64
import json
import xml.etree.ElementTree as ET
from typing import Any

from cryptography.exceptions import InvalidSignature
from cryptography.hazmat.primitives import hashes
from cryptography.hazmat.primitives.asymmetric import padding, rsa


def _number(root: ET.Element, name: str) -> int:
    return int.from_bytes(base64.b64decode(root.findtext(name)), "big")


def _public_numbers(root: ET.Element) -> rsa.RSAPublicNumbers:
    return rsa.RSAPublicNumbers(_number(root, "Exponent"), _number(root, "Modulus"))


def load_public_key(xml: str) -> rsa.RSAPublicKey:
    return _public_numbers(ET.fromstring(xml)).public_key()


def load_private_key(xml: str) -> rsa.RSAPrivateKey:
    root = ET.fromstring(xml)
    return rsa.RSAPrivateNumbers(
        p=_number(root, "P"),
        q=_number(root, "Q"),
        d=_number(root, "D"),
        dmp1=_number(root, "DP"),
        dmq1=_number(root, "DQ"),
        iqmp=_number(root, "InverseQ"),
        public_numbers=_public_numbers(root),
    ).private_key()


def public_key_xml(key: rsa.RSAPublicKey) -> str:
    numbers = key.public_numbers()

    def encode(value: int) -> str:
        return base64.b64encode(value.to_bytes((value.bit_length() + 7) // 8, "big")).decode()

    return f"<RSAKeyValue><Modulus>{encode(numbers.n)}</Modulus><Exponent>{encode(numbers.e)}</Exponent></RSAKeyValue>"


def sign(data: str, key: rsa.RSAPrivateKey) -> str:
    return base64.b64encode(key.sign(data.encode(), padding.PKCS1v15(), hashes.SHA256())).decode()


def verify(data: str, signature: str, key: rsa.RSAPublicKey) -> bool:
    try:
        key.verify(base64.b64decode(signature), data.encode(), padding.PKCS1v15(), hashes.SHA256())
    except InvalidSignature:
        return False
    return True


def generate_payment_request(payment_info: dict[str, Any], site_key: str, return_url: str) -> str:
    key = load_private_key(site_key)
    payment_info_json = json.dumps(payment_info)
    request = {
        "PaymentInfo": payment_info_json,
        "PaymentInfoSignature": sign(payment_info_json, key),
        "PublicKey": public_key_xml(key.public_key()),
        "ReturnUrl": return_url,
    }
    return base64.b64encode(json.dumps(request).encode()).decode()


def process_payment_result(data: str, site_key: str, central_api_key: str) -> dict[str, Any] | None:
    """Returns the verified PaymentInfo of a successful payment, or None."""
    try:
        result = json.loads(base64.b64decode(data, validate=True))
        info_json = result["PaymentInfo"]
        proof_json = result["PaymentProof"]
        confirmation_json = result["PaymentConfirmation"]
        confirmation_signature = result["PaymentConfirmationSignature"]

        info = json.loads(info_json)
        proof = json.loads(proof_json)
        confirmation = json.loads(confirmation_json)

        # The trust chain: our own signature on the PaymentInfo, then CentralApi vouching
        # for the bank's key, then the bank's signature on the confirmation.
        trusted = (
            verify(info_json, proof["PaymentInfoSignature"], load_private_key(site_key).public_key())
            and verify(proof_json, confirmation["PaymentProofSignature"], load_public_key(central_api_key))
            and verify(confirmation_json, confirmation_signature, load_public_key(proof["BankPublicKey"]))
        )
    except (KeyError, TypeError, ValueError, ET.ParseError):
        return None

    return info if trusted and confirmation.get("Success") is True else None
