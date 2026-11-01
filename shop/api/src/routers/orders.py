from collections.abc import AsyncIterator

import httpx
from fastapi import APIRouter, Depends, Form, HTTPException, status
from fastapi.responses import RedirectResponse
from sqlalchemy import select
from sqlalchemy.ext.asyncio import AsyncSession

from src.config import Settings, get_settings
from src.db import get_db
from src.http_client import get_http_client
from src.models import Order, PaymentAttempt, PaymentMethod, PaymentStatus, Product, User
from src.payments import generate_payment_request, process_payment_result
from src.reconcile import complete_order, reconcile_order
from src.schemas import CardPaymentIn, DirectPaymentOut, OrderIn, OrderOut
from src.security import current_user

router = APIRouter(prefix="/api", tags=["orders"])

RETURN_PATH = "api/payments/direct/confirmation"
PAYMENT_FAILED = "Payment failed. Please check your card details and try again."
PAYMENT_PROCESSING = "A payment for this order is still being processed"


def _destination(settings: Settings) -> dict[str, str]:
    return {
        "DestinationBankName": settings.destination_bank_name,
        "DestinationBankCountry": settings.destination_bank_country,
        "DestinationBankSwiftCode": settings.destination_bank_swift_code,
        "DestinationBankAccountUniqueId": settings.destination_bank_account_unique_id,
        "RecipientName": settings.recipient_name,
    }


async def _own_order(order_id: str, user: User, db: AsyncSession) -> Order:
    order = await db.get(Order, order_id)
    if order is None or order.user_id != user.id:
        raise HTTPException(status.HTTP_404_NOT_FOUND, "Order not found")
    return order


async def _pending_order(
    order_id: str, user: User, db: AsyncSession, client: httpx.AsyncClient, settings: Settings
) -> Order:
    order = await _own_order(order_id, user, db)
    # Paying again while an earlier payment may still arrive would charge the customer twice
    await reconcile_order(order, db, client, settings)
    if order.payment_status == PaymentStatus.processing:
        raise HTTPException(status.HTTP_409_CONFLICT, PAYMENT_PROCESSING)
    if order.payment_status != PaymentStatus.pending:
        raise HTTPException(status.HTTP_409_CONFLICT, "This order has already been paid")
    return order


async def _new_attempt(order: Order, method: PaymentMethod, db: AsyncSession) -> PaymentAttempt:
    attempt = PaymentAttempt(order_id=order.id, method=method)
    db.add(attempt)
    await db.commit()
    return attempt


@router.get("/orders", response_model=list[OrderOut])
async def list_orders(
    user: User = Depends(current_user),
    db: AsyncSession = Depends(get_db),
    settings: Settings = Depends(get_settings),
    client: httpx.AsyncClient = Depends(get_http_client),
) -> list[Order]:
    open_orders = select(Order).join(PaymentAttempt, PaymentAttempt.order_id == Order.id).where(Order.user_id == user.id)
    for order in list((await db.scalars(open_orders)).unique().all()):
        await reconcile_order(order, db, client, settings)

    query = select(Order).where(Order.user_id == user.id).order_by(Order.created_at.desc())
    return list((await db.scalars(query)).unique().all())


@router.post("/orders", response_model=OrderOut, status_code=status.HTTP_201_CREATED)
async def create_order(
    model: OrderIn, user: User = Depends(current_user), db: AsyncSession = Depends(get_db)
) -> Order:
    if await db.get(Product, model.product_id) is None:
        raise HTTPException(status.HTTP_404_NOT_FOUND, "Product not found")

    order = Order(product_id=model.product_id, user_id=user.id)
    db.add(order)
    await db.commit()
    return await db.get(Order, order.id, populate_existing=True)


@router.post("/orders/{order_id}/direct-payment", response_model=DirectPaymentOut)
async def start_direct_payment(
    order_id: str,
    user: User = Depends(current_user),
    db: AsyncSession = Depends(get_db),
    settings: Settings = Depends(get_settings),
    client: httpx.AsyncClient = Depends(get_http_client),
) -> DirectPaymentOut:
    order = await _pending_order(order_id, user, db, client, settings)
    # The same reference number in a second tab makes the bank refuse the second payment
    attempt = await db.scalar(
        select(PaymentAttempt).where(
            PaymentAttempt.order_id == order.id, PaymentAttempt.method == PaymentMethod.direct
        )
    ) or await _new_attempt(order, PaymentMethod.direct, db)
    payment_info = {
        "Amount": float(order.product.price),
        "Description": order.product.name,
        **_destination(settings),
        "OrderId": order.id,
        "ReferenceNumber": attempt.reference,
    }
    data = generate_payment_request(payment_info, settings.site_key, settings.site_url + RETURN_PATH)
    return DirectPaymentOut(url=settings.central_api_payment_url, field="data", data=data)


@router.post("/payments/direct/confirmation", include_in_schema=False)
async def receive_confirmation(
    data: str | None = Form(default=None),
    db: AsyncSession = Depends(get_db),
    settings: Settings = Depends(get_settings),
) -> RedirectResponse:
    payment_info = process_payment_result(data, settings.site_key, settings.central_api_public_key) if data else None
    order_id = payment_info.get("OrderId") if isinstance(payment_info, dict) else None
    if not isinstance(order_id, str):
        raise HTTPException(status.HTTP_400_BAD_REQUEST, "Invalid payment result")

    order = await db.get(Order, order_id)
    if order is not None and order.payment_status != PaymentStatus.completed:
        await complete_order(order, db)

    return RedirectResponse(settings.site_url + "cabinet/orders", status.HTTP_303_SEE_OTHER)


@router.post("/orders/{order_id}/card-payment", response_model=OrderOut)
async def pay_by_card(
    order_id: str,
    card: CardPaymentIn,
    user: User = Depends(current_user),
    db: AsyncSession = Depends(get_db),
    settings: Settings = Depends(get_settings),
    client: httpx.AsyncClient = Depends(get_http_client),
) -> Order:
    order = await _pending_order(order_id, user, db, client, settings)
    attempt = await _new_attempt(order, PaymentMethod.card, db)
    submission = {
        "Number": card.number,
        "Name": card.name,
        "ExpiryDate": card.expiry_date,
        "SecurityCode": card.security_code,
        "Amount": float(order.product.price),
        "Description": order.product.name,
        "ReferenceNumber": attempt.reference,
        **_destination(settings),
    }

    try:
        response = await client.post(settings.central_api_card_payments_url, json=submission)
    except httpx.HTTPError:
        response = None

    if response is not None and response.status_code == status.HTTP_200_OK:
        await complete_order(order, db)
        return order

    # A timeout, a server error and 202 all mean the money may yet move, so the attempt stays open
    if response is None or response.status_code == status.HTTP_202_ACCEPTED or response.status_code >= 500:
        order.payment_status = PaymentStatus.processing
        await db.commit()
        return order

    await db.delete(attempt)
    await db.commit()
    raise HTTPException(status.HTTP_402_PAYMENT_REQUIRED, PAYMENT_FAILED)
