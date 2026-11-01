package uz.abs.kyc;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertTrue;

import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import java.io.IOException;
import java.net.CookieManager;
import java.net.CookiePolicy;
import java.net.URI;
import java.net.URLDecoder;
import java.net.URLEncoder;
import java.net.http.HttpClient;
import java.net.http.HttpRequest;
import java.net.http.HttpResponse;
import java.nio.charset.StandardCharsets;
import java.util.Base64;
import java.util.LinkedHashMap;
import java.util.Map;
import java.util.UUID;
import java.util.regex.Pattern;
import java.util.stream.Collectors;

/** A browser and the flows every test needs: login, register and the token exchange. */
final class Kyc {
    static final String BASE = System.getenv().getOrDefault("KYC_URL", "http://kyc-proxy:5000");
    static final String REALM = BASE + "/realms/abs";
    static final String OIDC = REALM + "/protocol/openid-connect";
    static final String AUTH = OIDC + "/auth";
    static final String TOKEN = OIDC + "/token";
    static final String USERINFO = OIDC + "/userinfo";
    static final String LOGOUT = OIDC + "/logout";
    static final String DEMO = "demo@abs.uz";
    static final String PASSWORD = "123456";

    private static final ObjectMapper JSON = new ObjectMapper();
    private static final Pattern FORM_ACTION = Pattern.compile("<form[^>]*action=\"([^\"]+)\"");

    private final CookieManager cookies = new CookieManager(null, CookiePolicy.ACCEPT_ALL);
    private final HttpClient http = HttpClient.newBuilder()
            .version(HttpClient.Version.HTTP_1_1)
            .followRedirects(HttpClient.Redirect.NEVER)
            .cookieHandler(cookies)
            .build();

    record Response(int status, String body, String location) {
        JsonNode json() {
            try {
                return JSON.readTree(body);
            } catch (IOException e) {
                throw new AssertionError(body, e);
            }
        }
    }

    static String redirectUri(String client) {
        return switch (client) {
            case "bank" -> "http://localhost:8081/signin-oidc";
            case "shop" -> "http://localhost:8082/api/auth/callback";
            default -> throw new IllegalArgumentException(client);
        };
    }

    static String secret(String client) {
        return System.getenv(client.toUpperCase() + "_CLIENT_SECRET");
    }

    static String newEmail() {
        return UUID.randomUUID().toString().replace("-", "") + "@abs.uz";
    }

    static String authorizeUrl(String client, Map<String, String> extra) {
        var query = new LinkedHashMap<String, String>();
        query.put("client_id", client);
        query.put("redirect_uri", redirectUri(client));
        query.put("response_type", "code");
        query.put("scope", "openid email profile");
        query.put("state", UUID.randomUUID().toString());
        query.put("nonce", UUID.randomUUID().toString());
        query.putAll(extra);
        return AUTH + "?" + encode(query);
    }

    static JsonNode claims(String token) {
        try {
            return JSON.readTree(Base64.getUrlDecoder().decode(token.split("\\.")[1]));
        } catch (IOException e) {
            throw new AssertionError(token, e);
        }
    }

    Response get(String url) {
        return send(HttpRequest.newBuilder(URI.create(url)).GET());
    }

    Response get(String url, String bearer) {
        return send(HttpRequest.newBuilder(URI.create(url)).header("Authorization", "Bearer " + bearer).GET());
    }

    Response post(String url, Map<String, String> form) {
        return send(formRequest(url, form));
    }

    Response post(String url, Map<String, String> form, String client, String secret) {
        var basic = Base64.getEncoder().encodeToString((client + ":" + secret).getBytes(StandardCharsets.UTF_8));
        return send(formRequest(url, form).header("Authorization", "Basic " + basic));
    }

    void clearCookies() {
        cookies.getCookieStore().removeAll();
    }

    Response logIn(String client, String email, String password) {
        var page = get(authorizeUrl(client, Map.of()));
        return post(formAction(page), Map.of("username", email, "password", password));
    }

    Response logIn(String client, String email) {
        return logIn(client, email, PASSWORD);
    }

    Response register(String client, String email, String name, boolean terms) {
        var page = get(authorizeUrl(client, Map.of("prompt", "create")));
        var fields = new LinkedHashMap<String, String>();
        fields.put("email", email);
        fields.put("firstName", name);
        fields.put("password", PASSWORD);
        fields.put("password-confirm", PASSWORD);
        if (terms) {
            fields.put("termsAccepted", "on");
        }
        return post(formAction(page), fields);
    }

    Response register(String client, String email) {
        return register(client, email, "Test User", true);
    }

    Response exchange(String client, String code, String secret) {
        return post(TOKEN, Map.of("grant_type", "authorization_code", "code", code, "redirect_uri", redirectUri(client)),
                client, secret);
    }

    Response exchange(String client, String code) {
        return exchange(client, code, secret(client));
    }

    Response refresh(String client, String refreshToken) {
        return post(TOKEN, Map.of("grant_type", "refresh_token", "refresh_token", refreshToken), client, secret(client));
    }

    Response endSession(String client, String refreshToken) {
        return post(LOGOUT, Map.of("refresh_token", refreshToken), client, secret(client));
    }

    JsonNode tokens(String client) {
        return exchange(client, codeOf(logIn(client, DEMO))).json();
    }

    static String codeOf(Response response) {
        assertEquals(302, response.status(), response.body());
        var query = URI.create(response.location()).getRawQuery();
        for (var pair : query.split("&")) {
            if (pair.startsWith("code=")) {
                return URLDecoder.decode(pair.substring(5), StandardCharsets.UTF_8);
            }
        }
        throw new AssertionError("No code in " + response.location());
    }

    static String formAction(Response page) {
        var match = FORM_ACTION.matcher(page.body());
        assertTrue(match.find(), page.body());
        return match.group(1).replace("&amp;", "&");
    }

    private HttpRequest.Builder formRequest(String url, Map<String, String> form) {
        return HttpRequest.newBuilder(URI.create(url))
                .header("Content-Type", "application/x-www-form-urlencoded")
                .POST(HttpRequest.BodyPublishers.ofString(encode(form)));
    }

    private Response send(HttpRequest.Builder request) {
        try {
            var response = http.send(request.build(), HttpResponse.BodyHandlers.ofString());
            return new Response(response.statusCode(), response.body(),
                    response.headers().firstValue("location").orElse(""));
        } catch (IOException e) {
            throw new AssertionError(e);
        } catch (InterruptedException e) {
            Thread.currentThread().interrupt();
            throw new AssertionError(e);
        }
    }

    private static String encode(Map<String, String> values) {
        return values.entrySet().stream()
                .map(e -> e.getKey() + "=" + URLEncoder.encode(e.getValue(), StandardCharsets.UTF_8))
                .collect(Collectors.joining("&"));
    }
}
