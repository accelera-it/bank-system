import { expect, fillLogin, fillRegistration, LAPTOP, test } from "./fixtures";

test("home shows shop and navigation", async ({ page }) => {
  await page.goto("/");

  await expect(page.getByRole("heading", { level: 1 })).toBeVisible();
  await expect(page.getByRole("link", { name: "Products", exact: true })).toBeVisible();
  await expect(page.getByRole("link", { name: "Login" })).toBeVisible();
  await expect(page.getByRole("link", { name: "Get started" })).toHaveCount(0);
  await expect(page.getByText(LAPTOP)).toBeVisible();
});

test("catalog opens product page", async ({ page }) => {
  await page.goto("/products");
  await page.getByRole("link", { name: LAPTOP, exact: true }).last().click();

  await expect(page.getByRole("heading", { name: LAPTOP })).toBeVisible();
  await expect(page.getByText("8 900 000 UZS")).toBeVisible();
});

test("login with demo user goes to keycloak and comes back", async ({ page }) => {
  await page.goto("/");
  await page.getByRole("link", { name: "Login" }).click();
  await expect(page).toHaveURL(/localhost:8080\/realms\/abs/);
  await fillLogin(page, "demo@abs.uz");

  await expect(page).toHaveURL("/cabinet/dashboard");
  await expect(page.getByText("ABS Admin")).toBeVisible();
});

test("wrong password shows the keycloak error", async ({ page }) => {
  await page.goto("/api/auth/login");
  await fillLogin(page, "demo@abs.uz", "wrong-password");

  await expect(page.getByText(/Invalid/).first()).toBeVisible();
});

test("keycloak login asks only for email and password", async ({ page }) => {
  await page.goto("/api/auth/login");

  await expect(page.locator("form input:visible")).toHaveCount(3);
  await expect(page.locator("#username")).toBeVisible();
  await expect(page.locator("#password")).toBeVisible();
});

test("register with taken email shows the keycloak error", async ({ page }) => {
  await page.goto("/api/auth/login?register=true");
  await fillRegistration(page, "demo@abs.uz");

  await expect(page.getByText(/already exists/).first()).toBeVisible();
});

test("guest buying signs in first and gets to payment", async ({ page, email }) => {
  await page.goto("/products");
  await page.locator("article").filter({ hasText: LAPTOP }).getByRole("button", { name: "Buy" }).click();
  await expect(page).toHaveURL(/localhost:8080\/realms\/abs/);

  await page.getByRole("link", { name: "Register" }).click();
  await fillRegistration(page, email);

  await expect(page.getByRole("dialog").getByText("Pay for")).toBeVisible();
});

test("header shows cabinet link when signed in", async ({ signedIn: page }) => {
  await page.goto("/");

  await expect(page.getByRole("link", { name: "Cabinet" })).toBeVisible();
  await expect(page.getByRole("link", { name: "Login" })).toHaveCount(0);
  await expect(page.getByRole("button", { name: "Log out" })).toHaveCount(0);
});

test("register shows full name first and waits for the agreement", async ({ page }) => {
  await page.goto("/api/auth/login?register=true");

  const names = await page.locator("form input:visible").evaluateAll((inputs) => inputs.map((input) => (input as HTMLInputElement).name));
  expect(names.slice(0, 5)).toEqual(["firstName", "email", "password", "password-confirm", "termsAccepted"]);
  await expect(page.locator("#phone")).toBeHidden();
  await expect(page.locator("#address")).toBeHidden();
  const submit = page.locator("#kc-register-form [type=submit]");
  await expect(submit).toBeDisabled();

  await page.locator("input[name=termsAccepted]").check();
  await expect(submit).toBeEnabled();
});
