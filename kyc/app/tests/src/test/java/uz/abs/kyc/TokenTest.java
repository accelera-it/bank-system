package uz.abs.kyc;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertNotEquals;
import static org.junit.jupiter.api.Assertions.assertTrue;

import java.util.Map;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.params.ParameterizedTest;
import org.junit.jupiter.params.provider.ValueSource;

class TokenTest {
    private final Kyc browser = new Kyc();

    @Test
    void discoveryNamesTheIssuer() {
        var config = browser.get(Kyc.REALM + "/.well-known/openid-configuration").json();

        assertEquals(Kyc.REALM, config.get("issuer").asText());
        assertEquals(Kyc.TOKEN, config.get("token_endpoint").asText());
    }

    @ParameterizedTest
    @ValueSource(strings = {"bank", "shop"})
    void codeGivesAllTheTokens(String client) {
        var tokens = browser.tokens(client);

        assertTrue(tokens.hasNonNull("access_token"));
        assertTrue(tokens.hasNonNull("refresh_token"));
        var idToken = Kyc.claims(tokens.get("id_token").asText());
        assertEquals(Kyc.REALM, idToken.get("iss").asText());
        assertEquals(client, idToken.get("aud").asText());
        assertEquals(Kyc.DEMO, idToken.get("email").asText());
    }

    @Test
    void codeWorksOnlyOnce() {
        var code = Kyc.codeOf(browser.logIn("shop", Kyc.DEMO));

        assertEquals(200, browser.exchange("shop", code).status());
        assertEquals(400, browser.exchange("shop", code).status());
    }

    @Test
    void wrongClientSecretIsRefused() {
        var code = Kyc.codeOf(browser.logIn("shop", Kyc.DEMO));

        assertEquals(401, browser.exchange("shop", code, "wrong").status());
    }

    @Test
    void codeOfOneClientIsUselessToTheOther() {
        var code = Kyc.codeOf(browser.logIn("shop", Kyc.DEMO));

        assertEquals(400, browser.exchange("bank", code).status());
    }

    @Test
    void refreshTokenGivesANewAccessToken() {
        var tokens = browser.tokens("shop");

        var refreshed = browser.refresh("shop", tokens.get("refresh_token").asText());

        assertEquals(200, refreshed.status());
        assertNotEquals(tokens.get("access_token").asText(), refreshed.json().get("access_token").asText());
    }

    @Test
    void passwordGrantIsOffForTheApps() {
        var form = Map.of("grant_type", "password", "username", Kyc.DEMO, "password", Kyc.PASSWORD);

        var status = browser.post(Kyc.TOKEN, form, "shop", Kyc.secret("shop")).status();

        assertTrue(status == 400 || status == 401, "status " + status);
    }

    @Test
    void badAccessTokenIsRefused() {
        assertEquals(401, browser.get(Kyc.USERINFO, "nonsense").status());
    }

    @Test
    void logoutEndsTheTokenSession() {
        var refreshToken = browser.tokens("shop").get("refresh_token").asText();

        assertEquals(204, browser.endSession("shop", refreshToken).status());

        assertEquals(400, browser.refresh("shop", refreshToken).status());
    }

    @Test
    void rootUserGetsAnAdminToken() {
        var token = browser.post(Kyc.BASE + "/realms/master/protocol/openid-connect/token",
                Map.of("grant_type", "password", "client_id", "admin-cli", "username", "admin", "password", "123456"))
                .json().get("access_token").asText();

        var users = browser.get(Kyc.BASE + "/admin/realms/abs/users?email=" + Kyc.DEMO, token).json();

        assertEquals(1, users.size());
        assertEquals(Kyc.DEMO, users.get(0).get("email").asText());
    }
}
