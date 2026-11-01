import { randomUUID } from "node:crypto";

import type { Page } from "@playwright/test";

import { expect, fillLogin, fillRegistration, LAPTOP, PASSWORD, test } from "./fixtures";

const PNG = Buffer.from(
  "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==",
  "base64",
);

async function fillCard(page: Page, number: string) {
  const dialog = page.getByRole("dialog");
  await dialog.getByRole("tab", { name: "Card" }).click();
  await dialog.getByLabel("Card number").fill(number);
  await dialog.getByLabel("Name on card").fill("ABS Admin");
  await dialog.getByLabel("Expires").fill("12/30");
  await dialog.getByLabel("CVV").fill("123");
  await dialog.getByRole("button", { name: /^Pay / }).click();
}

test("guest is sent to login and back", async ({ page, email }) => {
  await page.goto("/cabinet/products");

  await expect(page).toHaveURL(/localhost:8080\/realms\/abs/);
  await page.getByRole("link", { name: "Register" }).click();
  await fillRegistration(page, email);

  await expect(page).toHaveURL("/cabinet/products");
});

test("menu opens every section", async ({ signedIn: page }) => {
  await page.getByRole("menuitem", { name: "Products" }).click();
  await expect(page.getByRole("button", { name: "Create product" })).toBeVisible();

  await page.getByRole("menuitem", { name: "Orders" }).click();
  await expect(page.getByText("You have no orders yet")).toBeVisible();

  await page.getByRole("menuitem", { name: "Dashboard" }).click();
  await expect(page.getByText("Recent orders")).toBeVisible();
  await expect(page.getByRole("menuitem", { name: "Profile", exact: true })).toHaveCount(0);
});

test("sidebar shows the full name as plain text", async ({ signedIn: page }) => {
  const sidebar = page.getByRole("complementary");

  await expect(sidebar.getByText("Test User")).toBeVisible();
  await expect(sidebar.getByRole("link", { name: "Test User" })).toHaveCount(0);
});

test("edit profile opens a keycloak window and updates the cabinet", async ({ signedIn: page, email }) => {
  const opened = page.waitForEvent("popup");
  await page.getByRole("menuitem", { name: "Edit profile" }).click();
  const popup = await opened;

  await expect(popup).toHaveURL(/localhost:8080\/realms\/abs/);
  await expect(popup.locator("#phone")).toHaveAttribute("type", "tel");
  await expect(popup.locator("textarea#address")).toBeVisible();
  await expect(popup.locator("#email")).toHaveValue(email);
  await expect(popup.locator("#email")).not.toBeEditable();
  await popup.locator("#firstName").fill("New Name");
  await popup.locator("form [type=submit]").first().click();

  await popup.waitForEvent("close");
  await expect(page).toHaveURL(/localhost:8080\/realms\/abs/);
  await fillLogin(page, email);
  await expect(page.getByRole("complementary").getByText("New Name")).toBeVisible();
});

test("change password asks for the current one and signs in again", async ({ signedIn: page, email }) => {
  const opened = page.waitForEvent("popup");
  await page.getByRole("menuitem", { name: "Change password" }).click();
  const popup = await opened;

  await popup.locator("#password-current").fill("wrong-password");
  await popup.locator("#password-new").fill("New-Passw0rd");
  await popup.locator("#password-confirm").fill("New-Passw0rd");
  await popup.locator("form [type=submit]").first().click();
  await expect(popup.getByText("The current password is wrong")).toBeVisible();

  await popup.locator("#password-current").fill(PASSWORD);
  await popup.locator("#password-new").fill("New-Passw0rd");
  await popup.locator("#password-confirm").fill("New-Passw0rd");
  await popup.locator("form [type=submit]").first().click();

  await popup.waitForEvent("close");
  await expect(page).toHaveURL(/localhost:8080\/realms\/abs/);
  await fillLogin(page, email, "New-Passw0rd");
  await expect(page).toHaveURL("/cabinet/dashboard");
});

test("a product can be edited and deleted", async ({ signedIn: page }) => {
  const name = `Keyboard ${randomUUID().slice(0, 8)}`;
  await page.getByRole("menuitem", { name: "Products" }).click();
  await page.getByRole("button", { name: "Create product" }).click();
  const dialog = page.getByRole("dialog");
  await dialog.getByLabel("Name").fill(name);
  await dialog.getByLabel("Price").fill("1500");
  await dialog.locator("input[type=file]").setInputFiles({ name: "keyboard.png", mimeType: "image/png", buffer: PNG });
  await expect(dialog.locator("img")).toBeVisible();
  await dialog.getByRole("button", { name: "Create" }).click();

  const row = (text: string) => page.getByRole("row").filter({ hasText: text });
  await row(name).getByRole("button", { name: "Edit" }).click();
  await dialog.getByLabel("Name").fill(`${name} v2`);
  await dialog.getByRole("button", { name: "Save" }).click();
  await expect(row(`${name} v2`)).toBeVisible();

  await row(`${name} v2`).getByRole("button", { name: "Delete" }).click();
  await page.getByRole("tooltip").getByRole("button", { name: "Delete" }).click();
  await expect(row(name)).toHaveCount(0);
});

test("a product with orders stays", async ({ pendingOrder: page }) => {
  await page.getByRole("button", { name: "Close" }).click();
  await page.getByRole("menuitem", { name: "Products" }).click();

  await page.getByRole("row").filter({ hasText: LAPTOP }).getByRole("button", { name: "Delete" }).click();
  await page.getByRole("tooltip").getByRole("button", { name: "Delete" }).click();

  await expect(page.getByText("This product has orders")).toBeVisible();
  await expect(page.getByRole("cell", { name: LAPTOP })).toBeVisible();
});

test("brand opens the site", async ({ signedIn: page }) => {
  await page.getByRole("link", { name: "Shop" }).first().click();

  await expect(page).toHaveURL("/");
  await expect(page.getByRole("link", { name: "Cabinet" })).toBeVisible();
});

test("dashboard shows what is waiting and what was paid", async ({ pendingOrder: page }) => {
  await page.getByRole("button", { name: "Close" }).click();
  await page.getByRole("menuitem", { name: "Dashboard" }).click();
  await expect(page.getByText("1 order is waiting for payment")).toBeVisible();

  await page.getByRole("button", { name: "Pay now" }).click();
  await fillCard(page, "1234567890123456");
  await expect(page.getByText("Payment completed")).toBeVisible();

  await page.getByRole("menuitem", { name: "Dashboard" }).click();
  const stat = (title: string) => page.locator(".ant-statistic").filter({ hasText: title });
  await expect(stat("Paid")).toContainText("1");
  await expect(stat("Pending")).toContainText("0");
  await expect(stat("Total spent")).toContainText("8 900 000 UZS");
  await expect(page.getByText("waiting for payment")).toHaveCount(0);
  await expect(page.getByRole("cell", { name: LAPTOP })).toBeVisible();
});

test("card payment marks order paid", async ({ pendingOrder: page }) => {
  await fillCard(page, "1234567890123456");

  await expect(page.getByText("Payment completed")).toBeVisible();
  await expect(page.getByRole("cell", { name: "Paid" })).toBeVisible();
  await expect(page.getByRole("button", { name: "Pay" })).toHaveCount(0);
});

test("declined card keeps order pending", async ({ pendingOrder: page }) => {
  await fillCard(page, "0000000000000000");

  await expect(page.getByRole("dialog").getByText("Payment failed")).toBeVisible();
  await page.getByRole("button", { name: "Close" }).click();
  await expect(page.getByRole("cell", { name: "Pending" })).toBeVisible();
});

test("unconfirmed card payment shows processing and cannot be paid again", async ({ pendingOrder: page }) => {
  await fillCard(page, "2222000000000000");

  await expect(page.getByText("Payment is being processed")).toBeVisible();
  await expect(page.getByRole("cell", { name: "Processing" })).toBeVisible();
  await expect(page.getByRole("button", { name: "Pay" })).toHaveCount(0);
});

test("card form validates before sending", async ({ pendingOrder: page }) => {
  await fillCard(page, "123");

  await expect(page.getByText("Enter the 16 digits of the card")).toBeVisible();
});

test("bank payment posts to central api", async ({ pendingOrder: page }) => {
  await page.getByRole("dialog").getByRole("button", { name: "Continue to the bank" }).click();

  await expect(page.getByRole("heading", { name: "Stub bank" })).toBeVisible();
});

test("created product appears on the site", async ({ signedIn: page }) => {
  const name = `Headphones ${randomUUID().slice(0, 8)}`;
  await page.getByRole("menuitem", { name: "Products" }).click();
  await page.getByRole("button", { name: "Create product" }).click();

  const dialog = page.getByRole("dialog");
  await dialog.getByLabel("Name").fill(name);
  await dialog.getByLabel("Price").fill("1500");
  await dialog.locator("input[type=file]").setInputFiles({ name: "headphones.png", mimeType: "image/png", buffer: PNG });
  await expect(dialog.locator("img")).toBeVisible();
  await dialog.getByRole("button", { name: "Create" }).click();

  await expect(page.getByRole("cell", { name })).toBeVisible();
  await page.goto("/products");
  await expect(page.getByText(name)).toBeVisible();

  // The site really loads the picture from MinIO.
  const image = page.getByRole("img", { name });
  await expect(image).toHaveJSProperty("complete", true);
  expect(await image.evaluate((element: HTMLImageElement) => element.naturalWidth)).toBe(1);
});

test("logout ends the session", async ({ signedIn: page }) => {
  await page.getByRole("button", { name: "Log out" }).click();
  await expect(page.getByRole("link", { name: "Login" })).toBeVisible();

  await page.goto("/cabinet");
  await expect(page).toHaveURL(/localhost:8080\/realms\/abs/);
});

test("laptop name is shown in orders", async ({ pendingOrder: page }) => {
  await page.getByRole("button", { name: "Close" }).click();

  await expect(page.getByRole("cell", { name: LAPTOP })).toBeVisible();
});
