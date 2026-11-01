package uz.abs.kyc;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertTrue;

import java.util.Map;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.params.ParameterizedTest;
import org.junit.jupiter.params.provider.ValueSource;

class RegisterTest {
    private final Kyc browser = new Kyc();
    private final String email = Kyc.newEmail();

    @ParameterizedTest
    @ValueSource(strings = {"bank", "shop"})
    void registerSignsTheUserInWithTheFullName(String client) {
        var tokens = browser.exchange(client, Kyc.codeOf(browser.register(client, email))).json();

        var idToken = Kyc.claims(tokens.get("id_token").asText());
        assertEquals(email, idToken.get("email").asText());
        assertEquals("Test User", idToken.get("name").asText());
    }

    @Test
    void registeredUserCanLogInFromTheOtherClient() {
        Kyc.codeOf(browser.register("bank", email));
        browser.clearCookies();

        assertFalse(Kyc.codeOf(browser.logIn("shop", email)).isEmpty());
    }

    @Test
    void registerNeedsTheAgreement() {
        assertEquals(200, browser.register("shop", email, "Test User", false).status());

        browser.clearCookies();
        assertEquals(200, browser.logIn("shop", email).status());
    }

    @Test
    void registerPageHasTheAgreementAndNoLastName() {
        var page = browser.get(Kyc.authorizeUrl("shop", Map.of("prompt", "create"))).body();

        assertTrue(page.contains("name=\"termsAccepted\""));
        assertTrue(page.contains("name=\"firstName\""));
        assertTrue(page.contains("Full Name"));
        assertFalse(page.contains("name=\"lastName\""));
    }

    @Test
    void takenEmailIsRefused() {
        assertEquals(200, browser.register("shop", Kyc.DEMO).status());
    }

    @Test
    void registerNeedsAFullName() {
        assertEquals(200, browser.register("shop", email, "", true).status());
    }

    @Test
    void userinfoMatchesTheRegistration() {
        var tokens = browser.exchange("shop", Kyc.codeOf(browser.register("shop", email))).json();

        var info = browser.get(Kyc.USERINFO, tokens.get("access_token").asText()).json();

        assertEquals(email, info.get("email").asText());
        assertEquals("Test User", info.get("name").asText());
    }
}
