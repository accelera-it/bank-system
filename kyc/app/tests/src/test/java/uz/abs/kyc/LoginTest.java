package uz.abs.kyc;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertTrue;

import java.util.Map;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.params.ParameterizedTest;
import org.junit.jupiter.params.provider.ValueSource;

class LoginTest {
    private final Kyc browser = new Kyc();

    @ParameterizedTest
    @ValueSource(strings = {"bank", "shop"})
    void defaultUserLogsInOnBothClients(String client) {
        assertFalse(Kyc.codeOf(browser.logIn(client, Kyc.DEMO)).isEmpty());
    }

    @Test
    void wrongPasswordIsRefused() {
        var response = browser.logIn("shop", Kyc.DEMO, "wrong-password");

        assertEquals(200, response.status());
        assertTrue(response.body().contains("Invalid"));
    }

    @Test
    void unknownUserIsRefused() {
        assertEquals(200, browser.logIn("shop", "nobody@abs.uz").status());
    }

    @Test
    void loginOnOneClientIsSharedWithTheOther() {
        Kyc.codeOf(browser.logIn("bank", Kyc.DEMO));

        assertFalse(Kyc.codeOf(browser.get(Kyc.authorizeUrl("shop", Map.of()))).isEmpty());
    }

    @Test
    void unknownRedirectIsRefused() {
        var url = Kyc.authorizeUrl("shop", Map.of("redirect_uri", "http://evil.test/callback"));

        assertEquals(400, browser.get(url).status());
    }
}
