FROM python:3.13-slim
WORKDIR /app
ENV TZ=Asia/Tashkent \
    PYTHONUNBUFFERED=1
COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt
COPY src src
EXPOSE 5000
CMD ["sh", "-c", "python -m src.seed && exec uvicorn src.main:app --host 0.0.0.0 --port 5000"]
