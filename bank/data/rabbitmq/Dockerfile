FROM rabbitmq:4.3-alpine

ENV TZ=Asia/Tashkent

COPY rabbitmq.conf definitions.json /etc/rabbitmq/

EXPOSE 5672

# As root, the image leaves a cookie on an empty volume that RabbitMQ cannot read
USER rabbitmq
