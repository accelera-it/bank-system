import { randomUUID } from "node:crypto";

import { expect, type Page, test as base } from "@playwright/test";

export const PASSWORD = "123456";
export const LAPTOP = 'Laptop 14" Core i7, 16 GB, 512 GB SSD';

export async function fillRegistration(page: Page, email: string) {
  await page.getByLabel("Full Name").fill("Test User");
  await page.getByLabel("Email").fill(email);
  await page.getByLabel("Password", { exact: true }).fill(PASSWORD);
  await page.getByLabel("Confirm password").fill(PASSWORD);
  await page.getByLabel("I accept").check();
  await page.getByRole("button", { name: "Create account" }).click();
}

interface Fixtures {
  email: string;
  signedIn: Page;
  pendingOrder: Page;
}

export const test = base.extend<Fixtures>({
  email: async ({}, use) => {
    await use(`${randomUUID().replaceAll("-", "")}@abs.uz`);
  },

  signedIn: async ({ page, email }, use) => {
    await page.goto("/register");
    await fillRegistration(page, email);
    await expect(page).toHaveURL("/cabinet/dashboard");
    await use(page);
  },

  // Buys the laptop on the site, which leaves the browser on the payment popup.
  pendingOrder: async ({ signedIn: page }, use) => {
    await page.goto("/products");
    await page.locator("article").filter({ hasText: LAPTOP }).getByRole("button", { name: "Buy" }).click();
    await expect(page.getByRole("dialog")).toBeVisible();
    await use(page);
  },
});

export { expect };
