import { randomUUID } from "node:crypto";

import { expect, type Page, test as base } from "@playwright/test";

export const PASSWORD = "123456";
export const LAPTOP = 'Laptop 14" Core i7, 16 GB, 512 GB SSD';

export async function fillRegistration(page: Page, email: string) {
  await page.locator("#firstName").fill("Test User");
  await page.locator("#email").fill(email);
  await page.locator("#password").fill(PASSWORD);
  await page.locator("#password-confirm").fill(PASSWORD);
  await page.locator("input[name=termsAccepted]").check();
  await page.locator("#kc-register-form [type=submit]").click();
}

export async function fillLogin(page: Page, email: string, password = PASSWORD) {
  await page.locator("#username").fill(email);
  await page.locator("#password").fill(password);
  await page.locator("#kc-login").click();
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
    await page.goto("/api/auth/login?register=true");
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
