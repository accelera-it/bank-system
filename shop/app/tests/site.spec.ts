import { expect, fillRegistration, LAPTOP, PASSWORD, test } from "./fixtures";

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

test("login with demo user opens cabinet", async ({ page }) => {
  await page.goto("/login");
  await page.getByLabel("Email").fill("demo@abs.uz");
  await page.getByLabel("Password").fill(PASSWORD);
  await page.getByRole("button", { name: "Log in" }).click();

  await expect(page).toHaveURL("/cabinet/dashboard");
  await expect(page.getByText("Demo User")).toBeVisible();
});

test("wrong password shows error", async ({ page }) => {
  await page.goto("/login");
  await page.getByLabel("Email").fill("demo@abs.uz");
  await page.getByLabel("Password").fill("wrong-password");
  await page.getByRole("button", { name: "Log in" }).click();

  await expect(page.getByRole("alert")).toHaveText("Invalid email or password");
});

test("register with taken email shows error", async ({ page }) => {
  await page.goto("/register");
  await fillRegistration(page, "demo@abs.uz");

  await expect(page.getByRole("alert")).toHaveText("This email is already registered");
});

test("guest buying signs in first and gets to payment", async ({ page, email }) => {
  await page.goto("/products");
  await page.locator("article").filter({ hasText: LAPTOP }).getByRole("button", { name: "Buy" }).click();
  await expect(page).toHaveURL(/\/login\?buy=/);

  await page.getByRole("link", { name: "Create an account" }).click();
  await fillRegistration(page, email);

  await expect(page.getByRole("dialog").getByText("Pay for")).toBeVisible();
});

test("header shows cabinet link when signed in", async ({ signedIn: page }) => {
  await page.goto("/");

  await expect(page.getByRole("link", { name: "Cabinet" })).toBeVisible();
  await expect(page.getByRole("link", { name: "Login" })).toHaveCount(0);
  await expect(page.getByRole("button", { name: "Log out" })).toHaveCount(0);
});

test("register button waits for the agreement", async ({ page }) => {
  await page.goto("/register");
  const submit = page.getByRole("button", { name: "Create account" });
  await expect(submit).toBeDisabled();

  await page.getByLabel("I accept").check();
  await expect(submit).toBeEnabled();
});

test("register checks that the passwords match", async ({ page, email }) => {
  await page.goto("/register");
  await page.getByLabel("Full Name").fill("Test User");
  await page.getByLabel("Email").fill(email);
  await page.getByLabel("Password", { exact: true }).fill(PASSWORD);
  await page.getByLabel("Confirm password").fill("different");
  await page.getByLabel("I accept").check();
  await page.getByRole("button", { name: "Create account" }).click();

  await expect(page.getByRole("alert")).toHaveText("Passwords do not match");
});
