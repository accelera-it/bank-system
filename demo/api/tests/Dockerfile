# Build context: demo/api (see demo/docker-compose.test.yml)
FROM python:3.13-slim
WORKDIR /api
COPY . .
RUN pip install --no-cache-dir -r tests/requirements.txt
WORKDIR /api/tests
ENTRYPOINT ["pytest", "-q"]
