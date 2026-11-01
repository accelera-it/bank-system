from functools import lru_cache
from io import BytesIO

from minio import Minio
from minio.error import S3Error

from src.config import get_settings


class Storage:
    """Product images in a private MinIO bucket."""

    def __init__(self, client: Minio, bucket: str) -> None:
        self.client = client
        self.bucket = bucket

    def ensure_bucket(self) -> None:
        if not self.client.bucket_exists(self.bucket):
            self.client.make_bucket(self.bucket)

    def put(self, key: str, data: bytes, content_type: str) -> None:
        self.client.put_object(self.bucket, key, BytesIO(data), len(data), content_type=content_type)

    def get(self, key: str) -> tuple[bytes, str] | None:
        try:
            response = self.client.get_object(self.bucket, key)
        except S3Error as error:
            if error.code == "NoSuchKey":
                return None
            raise
        try:
            return response.read(), response.headers["Content-Type"]
        finally:
            response.close()
            response.release_conn()


@lru_cache
def get_storage() -> Storage:
    settings = get_settings()
    client = Minio(
        settings.minio_endpoint,
        access_key=settings.minio_root_user,
        secret_key=settings.minio_root_password,
        secure=False,
    )
    return Storage(client, settings.minio_bucket)
