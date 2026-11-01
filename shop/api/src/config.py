from functools import lru_cache

from pydantic_settings import BaseSettings


class Settings(BaseSettings):
    database_url: str = "postgresql+asyncpg://demoshop:demoshop@localhost:5432/DemoShop"
    redis_url: str = "redis://localhost:6379/0"
    minio_endpoint: str = "localhost:9000"
    minio_root_user: str = ""
    minio_root_password: str = ""
    minio_bucket: str = "products"
    session_days: int = 7
    products_cache_seconds: int = 60

    site_url: str = "http://localhost:8082/"
    kyc_authority: str = "http://localhost:8080/realms/abs"
    kyc_public_authority: str = "http://localhost:8080/realms/abs"
    kyc_client_id: str = "shop"
    shop_client_secret: str = ""
    site_key: str = ""
    central_api_public_key: str = ""
    central_api_payment_url: str = "http://localhost:8080/pay"
    central_api_card_payments_url: str = "http://localhost:8080/api/CardPayments"

    destination_bank_name: str = ""
    destination_bank_country: str = ""
    destination_bank_swift_code: str = ""
    destination_bank_account_unique_id: str = ""
    recipient_name: str = ""


@lru_cache
def get_settings() -> Settings:
    return Settings()
