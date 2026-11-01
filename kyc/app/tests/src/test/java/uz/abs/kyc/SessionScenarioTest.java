package uz.abs.kyc;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertNull;

import com.fasterxml.jackson.databind.JsonNode;
import java.net.URI;
import java.util.UUID;
import org.junit.jupiter.api.AfterEach;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.params.ParameterizedTest;
import org.junit.jupiter.params.provider.ValueSource;
import redis.clients.jedis.JedisPooled;
import redis.clients.jedis.params.SetParams;

/** What the bank and the shop do with a login: keep it in Redis, use it, renew it and end it. */
class SessionScenarioTest {
    private final JedisPooled redis = new JedisPooled(URI.create(System.getenv("REDIS_URL")));
    private final Kyc browser = new Kyc();
    private final String email = Kyc.newEmail();

    @AfterEach
    void closeRedis() {
        redis.close();
    }

    private String start(JsonNode tokens) {
        var session = UUID.randomUUID().toString();
        redis.set("session:" + session, tokens.toString(), SetParams.setParams().ex(60));
        return session;
    }

    private JsonNode read(String session) {
        var value = redis.get("session:" + session);
        return value == null ? null : new Kyc.Response(200, value, "").json();
    }

    private void replace(String session, JsonNode tokens) {
        redis.set("session:" + session, tokens.toString(), SetParams.setParams().keepTtl());
    }

    @ParameterizedTest
    @ValueSource(strings = {"bank", "shop"})
    void newUserFromRegisterToLogout(String client) {
        // Register, and keep the login the way an app does
        var session = start(browser.exchange(client, Kyc.codeOf(browser.register(client, email))).json());

        // A later request finds the user through Redis
        var info = browser.get(Kyc.USERINFO, read(session).get("access_token").asText()).json();
        assertEquals(email, info.get("email").asText());

        // The session outlives the access token
        var renewed = browser.refresh(client, read(session).get("refresh_token").asText());
        assertEquals(200, renewed.status());
        replace(session, renewed.json());
        assertEquals(email, Kyc.claims(read(session).get("id_token").asText()).get("email").asText());

        // Logout ends it in Keycloak and in Redis
        var refreshToken = read(session).get("refresh_token").asText();
        assertEquals(204, browser.endSession(client, refreshToken).status());
        redis.del("session:" + session);

        assertNull(read(session));
        assertEquals(400, browser.refresh(client, refreshToken).status());
    }

    @Test
    void oneUserKeepsOneSessionPerApp() {
        var bank = start(browser.exchange("bank", Kyc.codeOf(browser.register("bank", email))).json());
        browser.clearCookies();
        var shop = start(browser.exchange("shop", Kyc.codeOf(browser.logIn("shop", email))).json());

        var bankToken = Kyc.claims(read(bank).get("id_token").asText());
        var shopToken = Kyc.claims(read(shop).get("id_token").asText());

        assertEquals(bankToken.get("sub"), shopToken.get("sub"));
        assertEquals("bank", bankToken.get("aud").asText());
        assertEquals("shop", shopToken.get("aud").asText());
    }
}
