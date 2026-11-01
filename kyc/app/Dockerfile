FROM quay.io/keycloak/keycloak:26.5.0 AS build
ENV KC_DB=dev-file KC_HEALTH_ENABLED=true
RUN /opt/keycloak/bin/kc.sh build

FROM quay.io/keycloak/keycloak:26.5.0
ENV KC_DB=dev-file TZ=Asia/Tashkent
COPY --from=build /opt/keycloak/lib/quarkus/ /opt/keycloak/lib/quarkus/
COPY realms/ /opt/keycloak/data/import/
COPY themes/ /opt/keycloak/themes/
RUN mkdir -p /opt/keycloak/data/h2
EXPOSE 8080
CMD ["start", "--optimized", "--import-realm"]
