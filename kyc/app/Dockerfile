FROM maven:3.9-eclipse-temurin-21 AS provider
WORKDIR /provider
COPY provider/ .
RUN mvn -q -B package

FROM quay.io/keycloak/keycloak:26.5.0 AS build
COPY --from=provider /provider/target/kyc-provider-1.0.0.jar /opt/keycloak/providers/
ENV KC_DB=postgres KC_HEALTH_ENABLED=true
RUN /opt/keycloak/bin/kc.sh build

FROM quay.io/keycloak/keycloak:26.5.0
ENV KC_DB=postgres TZ=Asia/Tashkent
COPY --from=build /opt/keycloak/lib/quarkus/ /opt/keycloak/lib/quarkus/
COPY --from=build /opt/keycloak/providers/ /opt/keycloak/providers/
COPY realms/ /opt/keycloak/data/import/
COPY themes/ /opt/keycloak/themes/
COPY setup.sh /opt/keycloak/setup.sh
EXPOSE 8080
ENTRYPOINT ["bash", "/opt/keycloak/setup.sh"]
CMD ["start", "--optimized", "--import-realm"]
