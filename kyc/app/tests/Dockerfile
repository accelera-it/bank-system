FROM maven:3.9-eclipse-temurin-21
WORKDIR /tests
COPY pom.xml .
RUN mvn -q -B dependency:go-offline
COPY src src
ENTRYPOINT ["mvn", "-B", "test"]
