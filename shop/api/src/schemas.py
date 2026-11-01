from datetime import datetime
from decimal import Decimal
from typing import Annotated

from pydantic import BaseModel, ConfigDict, Field, StringConstraints, field_validator


class ProfileIn(BaseModel):
    name: Annotated[str, StringConstraints(strip_whitespace=True, min_length=1, max_length=100)]
    phone: Annotated[str, StringConstraints(strip_whitespace=True, max_length=30)] = ""
    address: Annotated[str, StringConstraints(strip_whitespace=True, max_length=300)] = ""


class UserOut(BaseModel):
    model_config = ConfigDict(from_attributes=True)

    id: str
    email: str
    name: str
    phone: str
    address: str


class ProductIn(BaseModel):
    name: str = Field(min_length=1, max_length=200)
    price: Decimal = Field(gt=0, max_digits=18, decimal_places=2)
    image_url: str = Field(min_length=1, max_length=500)


class ProductOut(BaseModel):
    model_config = ConfigDict(from_attributes=True)

    id: str
    name: str
    price: float
    image_url: str


class LogoutOut(BaseModel):
    url: str


class ImageOut(BaseModel):
    url: str


class OrderIn(BaseModel):
    product_id: str


class OrderOut(BaseModel):
    model_config = ConfigDict(from_attributes=True)

    id: str
    product: ProductOut
    payment_status: str
    created_at: datetime


class DirectPaymentOut(BaseModel):
    url: str
    field: str
    data: str


class CardPaymentIn(BaseModel):
    number: str = Field(pattern=r"^\d{16}$")
    name: str = Field(min_length=1, max_length=50)
    expiry_date: str = Field(pattern=r"^(0[1-9]|1[0-2])/\d{2}$")
    security_code: str = Field(pattern=r"^\d{3}$")

    @field_validator("name")
    @classmethod
    def strip_name(cls, value: str) -> str:
        return value.strip()
